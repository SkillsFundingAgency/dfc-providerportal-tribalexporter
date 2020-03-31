using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UkrlpService;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class FEChoicesDataMigrator
    {
        [FunctionName(nameof(FEChoicesDataMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IFEChoicesDataCollectionService feChoicesDataCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper
                    )
        {
            const string AppName = "FEChoicesData.Migrator";

            var stopWatch = new Stopwatch();

            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var feChoicesCollectionId = configuration["CosmosDbCollectionSettings:FEChoicesDataCollectionId"];
            var connectionString = configuration.GetConnectionString("TribalRestore");
            var fechoicesDataMigrationLogFile = $"FEChoicesDataMigrator-{DateTime.Now.ToString("dd-MM-yy HHmm")}";

            List<Guid> feDataRecordsToDeleteByGuid = new List<Guid>();
            List<int> feDataRecordsToDeleteByUkprn = new List<int>();

            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);

            var _cosmosClient = cosmosDbHelper.GetClient();

            // Get all changed data from UKRLP API
            stopWatch.Reset();
            log.LogInformation($"FEChoicesData Migrator: Start synching data..");

            var result = new List<FeChoicesDataResultMessage>();

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    // Get non duplicate UKPRN data only
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"SELECT	P.Ukprn, 
                                                        P.RecordStatusId,
                                                        D.UPIN, 
                                                        D.LearnerSatisfaction, 
                                                        D.LearnerDestination, 
                                                        D.EmployerSatisfaction,
                                                        D.CreatedDateTimeUtc,
                                                        Count(p.UKprn)
                                                 FROM [Provider] P
                                                 JOIN [FEChoices] D
                                                 ON P.UPIN = D.UPIN
                                                 WHERE p.RecordStatusId = 2
                                                 GROUP BY 
		                                                P.Ukprn, 
                                                        P.RecordStatusId,
                                                        D.UPIN, 
                                                        D.LearnerSatisfaction, 
                                                        D.LearnerDestination, 
                                                        D.EmployerSatisfaction,
                                                        D.CreatedDateTimeUtc
                                                HAVING Count(P.Ukprn) = 1
                                                ORDER BY D.CreatedDateTimeUtc DESC, p.Ukprn
                                            ";

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        stopWatch.Reset();
                        log.LogInformation($"Tribal Data: Start....");
                        stopWatch.Start();

                        List<FEChoicesSourceData> sourceData = new List<FEChoicesSourceData>();
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                // Read FE data from Tribal
                                var item = FEChoicesSourceData.FromDataReader(dataReader);
                                sourceData.Add(item);
                            }

                            dataReader.Close();
                        }

                        var destinationData = await feChoicesDataCollectionService.GetAllDocument();

                        var uniqueSourceUkprns = sourceData.Select(s => s.UKPRN).Distinct().ToList();

                        foreach (var ukprn in uniqueSourceUkprns)
                        {
                            // filter out duplicate form source
                            if(sourceData.Count(s => s.UKPRN == ukprn) > 1)
                            {
                                // mark for removal from destination if exists
                                feDataRecordsToDeleteByUkprn.Add(ukprn);
                                continue;
                            }

                            // pick the first as there might still be duplicates
                            var sourceItem = sourceData.First(s => s.UKPRN == ukprn);

                            try
                            {
                                // Check if item exists in both (could be duplicate in destination)
                                var itemsToUpdate = destinationData.Where(s => s.UKPRN == sourceItem.UKPRN);

                                // Update
                                if (itemsToUpdate != null)
                                {
                                    var itemToUpdate = itemsToUpdate.First();

                                    itemToUpdate.EmployerSatisfaction = sourceItem.EmployerSatisfaction;
                                    itemToUpdate.LearnerSatisfaction = sourceItem.LearnerSatisfaction;
                                    itemToUpdate.CreatedDateTimeUtc = sourceItem.CreatedDateTimeUtc;
                                    itemToUpdate.LastUpdatedBy = AppName;
                                    itemToUpdate.LastUpdatedOn = DateTime.UtcNow;

                                    Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, feChoicesCollectionId);
                                    await _cosmosClient.UpsertDocumentAsync(collectionUri, itemToUpdate);

                                    feDataRecordsToDeleteByGuid.AddRange(itemsToUpdate.Where(i => i.id != itemToUpdate.id).Select(i => i.id).ToList());

                                    AddResultMessage(sourceItem.UKPRN, "PROCESSED-Updated", $"Provider {sourceItem.UKPRN} updated in Cosmos Collection");
                                }
                                // Insert new entry
                                else
                                {
                                    var newRecord = new FEChoicesData()
                                    {
                                        id = Guid.NewGuid(),
                                        UKPRN = sourceItem.UKPRN,
                                        LearnerSatisfaction = sourceItem.LearnerSatisfaction,
                                        EmployerSatisfaction = sourceItem.EmployerSatisfaction,
                                        CreatedDateTimeUtc = sourceItem.CreatedDateTimeUtc,
                                        CreatedBy = AppName,
                                        CreatedOn = DateTime.UtcNow,
                                        LastUpdatedBy = AppName,
                                        LastUpdatedOn = DateTime.UtcNow,
                                    };

                                    await cosmosDbHelper.CreateDocumentAsync(_cosmosClient, feChoicesCollectionId, newRecord);

                                    AddResultMessage(sourceItem.UKPRN, "PROCESSED-Created", $"Provider {sourceItem.UKPRN} updated in Cosmos Collection");
                                }
                            }
                            catch (Exception ex)
                            {
                                AddResultMessage(sourceItem.UKPRN, "ERRORED", $"Error while inserting/updating for provider {ex.Message}");
                                log.LogError($"Tribal Data: Error processing data.", ex);
                            }
                        }

                        // Remove data that is not in source
                        var howManyToDelete = destinationData.Where(d => !sourceData.Select(s => s.UKPRN).Contains(d.UKPRN));
                        foreach (var existingItem in howManyToDelete)
                        {
                            Uri docUri = UriFactory.CreateDocumentUri(databaseId, feChoicesCollectionId, existingItem.id.ToString());
                            var deleteResult = await _cosmosClient.DeleteDocumentAsync(docUri, new RequestOptions() { PartitionKey = new PartitionKey(existingItem.UKPRN) });

                            AddResultMessage(existingItem.UKPRN, "DELETE", $"Record {existingItem.id} with UKPRN {existingItem.UKPRN} deleted as not in source.");
                        }

                        // Remove data that is duplicate in destination
                        var duplicatesToDeleteByGuid = destinationData.Where(d => feDataRecordsToDeleteByGuid.Contains(d.id));
                        foreach (var existingItem in duplicatesToDeleteByGuid)
                        {
                            Uri docUri = UriFactory.CreateDocumentUri(databaseId, feChoicesCollectionId, existingItem.id.ToString());
                            var deleteResult = await _cosmosClient.DeleteDocumentAsync(docUri, new RequestOptions() { PartitionKey = new PartitionKey(existingItem.UKPRN) });

                            AddResultMessage(existingItem.UKPRN, "DELETE", $"Record {existingItem.id} with UKPRN {existingItem.UKPRN} deleted as duplicate in Cosmos.");
                        }

                        // Remove data that is duplicate in source so needs to be removed from destination
                        var duplicatesToDeleteByUkprn = destinationData.Where(d => feDataRecordsToDeleteByUkprn.Contains(d.UKPRN));
                        foreach (var existingItem in duplicatesToDeleteByUkprn)
                        {
                            Uri docUri = UriFactory.CreateDocumentUri(databaseId, feChoicesCollectionId, existingItem.id.ToString());
                            var deleteResult = await _cosmosClient.DeleteDocumentAsync(docUri, new RequestOptions() { PartitionKey = new PartitionKey(existingItem.UKPRN) });

                            AddResultMessage(existingItem.UKPRN, "DELETE", $"Record {existingItem.id} with UKPRN {existingItem.UKPRN} deleted as duplicate in Cosmos.");
                        }

                    }
                    catch (Exception ex)
                    {
                        AddResultMessage(-1, "ERRORED-Unknown", $"{ex.Message}");
                        log.LogError($"Tribal Data: Error processing data.", ex);
                    }

                    stopWatch.Stop();
                    log.LogInformation($"Tribal Data: Processing completed in {stopWatch.ElapsedMilliseconds / 1000}");

                    var resultsObjBytes = GetResultAsByteArray(result);
                    await WriteResultsToBlobStorage(resultsObjBytes);
                }
            }

            void AddResultMessage(int providerId, string status, string message = "")
            {
                var validateResult = new FeChoicesDataResultMessage() { ProviderId = providerId, Status = status, Message = message };
                result.Add(validateResult);
            }

            double? FixData(double? valueToFix)
            {
                var valueToReturn = valueToFix;

                if(valueToFix == 0)
                {
                    valueToReturn = null;
                }
                else if(valueToFix > 10 && valueToFix < 100)
                {
                    valueToReturn = valueToFix / 10;
                }
                else if (valueToFix < 0 || valueToFix > 100)
                {
                    valueToReturn = null;
                }

                return valueToReturn;
            }


            byte[] GetResultAsByteArray(IList<FeChoicesDataResultMessage> message)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<FeChoicesDataResultMessage>(message);
                    }
                    return memoryStream.ToArray();
                }
            }

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, fechoicesDataMigrationLogFile, data);
            }
        }
    }
}

[Serializable()]
public class FeChoicesDataResultMessage
{
    public int ProviderId { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}

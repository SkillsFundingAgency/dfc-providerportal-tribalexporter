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
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"SELECT	P.ProviderId,
		                                            P.Ukprn, 
		                                            D.UPIN, 
		                                            D.LearnerSatisfaction, 
		                                            D.LearnerDestination, 
		                                            D.EmployerSatisfaction,
		                                            D.CreatedDateTimeUtc
                                            FROM [Provider] P
                                            JOIN [FEChoices] D
                                            ON P.UPIN = D.UPIN
                                            ORDER BY P.Ukprn
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

                        foreach (var sourceItem in sourceData)
                        {
                            // Check if item exists in both
                            var itemToUpdate = destinationData.SingleOrDefault(s => s.UKPRN == sourceItem.UKPRN);

                            // Update
                            if (itemToUpdate != null)
                            {
                                itemToUpdate.EmployerSatisfaction = sourceItem.EmployerSatisfaction;
                                itemToUpdate.LearnerSatisfaction = sourceItem.LearnerSatisfaction;
                                itemToUpdate.CreatedDateTimeUtc = sourceItem.CreatedDateTimeUtc;
                                itemToUpdate.LastUpdatedBy = AppName;
                                itemToUpdate.LastUpdatedOn = DateTime.UtcNow;

                                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, feChoicesCollectionId);
                                await _cosmosClient.UpsertDocumentAsync(collectionUri, itemToUpdate);

                                AddResultMessage(sourceItem.ProviderId, "PROCESSED-Updated", $"Provider {sourceItem.UKPRN} updated in Cosmos Collection");
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

                                AddResultMessage(sourceItem.ProviderId, "PROCESSED-Created", $"Provider {sourceItem.UKPRN} updated in Cosmos Collection");
                            }
                        }

                        // Remove data that is not in source
                        foreach(var existingItem in destinationData.Where(d => !sourceData.Select(s => s.UKPRN).Contains(d.UKPRN)))
                        {
                            Uri docUri = UriFactory.CreateDocumentUri(databaseId, feChoicesCollectionId, existingItem.id.ToString());
                            var deleteResult = await _cosmosClient.DeleteDocumentAsync(docUri, new RequestOptions() { PartitionKey = new PartitionKey(existingItem.UKPRN) });

                            AddResultMessage(-1, "PROCESSED-Deleted", $"Provider {existingItem.UKPRN} delted in cosmos collection.");
                        }

                    }
                    catch (Exception ex)
                    {
                        AddResultMessage(-1, "PROCESSED-Errored", $"{ex.Message}");
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

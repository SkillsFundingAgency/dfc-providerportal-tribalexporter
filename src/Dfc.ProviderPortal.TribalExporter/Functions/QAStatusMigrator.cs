using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Dapper;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class QAStatusMigrator
    {
        [FunctionName(nameof(QAStatusMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory)
        {

            var tribalConnectionString = configuration.GetConnectionString("TribalRestore");
            var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
            var qaStatuses = new List<ProviderQAStatus>();
            var cosmosDbClient = cosmosDbHelper.GetClient();
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var ukrlp = "ukrlp";
            var apprenticehipsUri = "apprenticeship";
            var logFileName = $"QAStatusMigrator-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var whitelistFileName = "ProviderWhiteList.txt";
            var whitelist = await GetProviderWhiteList();

            using (var sqlConnection = new SqlConnection(tribalConnectionString))
            {
                var sql = @"SELECT DISTINCT PassedOverallQAChecks,Ukprn
                            FROM [Provider]";
                qaStatuses = sqlConnection.Query<ProviderQAStatus>(sql).ToList();
            }

            try
            {
                using (var logStream = new MemoryStream())
                using (var logStreamWriter = new StreamWriter(logStream))
                using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
                {
                    logCsvWriter.WriteField("UKPRN");
                    logCsvWriter.WriteField("CosmosID");
                    logCsvWriter.WriteField("PassedOverallQAChecks");
                    logCsvWriter.WriteField("Message");
                    logCsvWriter.NextRecord();

                    using (var sqlConnection = new SqlConnection(defaultConnectionString))
                    {
                        foreach (var s in qaStatuses)
                        {
                            var message = "";
                            if (!whitelist.Contains(s.UKPRN))
                            {
                                continue;
                            }

                            var provider = await GetExistingProvider(s.UKPRN.ToString(), cosmosDbClient);
                            if (s.PassedOverallQAChecks && provider != null)
                            {
                                var sql = @"IF NOT EXISTS (SELECT 1 FROM [Pttcd].[Providers] WHERE ProviderID = @ID) 
                                            INSERT INTO [Pttcd].[Providers] (ProviderId,ApprenticeshipQAStatus) SELECT @ID,@Status
                                        ELSE
                                            UPDATE [Pttcd].[Providers] SET ApprenticeshipQAStatus = @Status WHERE ProviderId = @ID";
                                var result = sqlConnection.Execute(sql, new
                                {
                                    ID = provider.id,
                                    Status = 16
                                });
                            }
                            else
                            {
                                var apprenticeships = await GetApprenticeships(s.UKPRN, cosmosDbClient);
                                message = $"Found {apprenticeships.Count()} Apprenticeship that is either Live or PendingMigration";
                            }

                            logCsvWriter.WriteField(s.UKPRN);
                            logCsvWriter.WriteField(provider?.id);
                            logCsvWriter.WriteField(s.PassedOverallQAChecks);
                            logCsvWriter.WriteField(message);
                            logCsvWriter.NextRecord();

                        }
                    }

                    // Upload log CSV to blob storage
                    {
                        logStreamWriter.Flush();

                        logStream.Seek(0L, SeekOrigin.Begin);

                        var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                        await blob.UploadFromStreamAsync(logStream);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw;
            }

            async Task<ISet<int>> GetProviderWhiteList()
            {
                var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(whitelistFileName);

                var ms = new MemoryStream();
                await blob.DownloadToStreamAsync(ms);
                ms.Seek(0L, SeekOrigin.Begin);

                var results = new HashSet<int>();
                string line;
                using (var reader = new StreamReader(ms))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        var ukprn = int.Parse(line);
                        results.Add(ukprn);
                    }
                }

                return results;
            }

            async Task<List<Apprenticeship>> GetApprenticeships(int ukprn, IDocumentClient documentClient)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticehipsUri);
                var apprenticeships = new List<Apprenticeship>();
                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation
                    };

                    var queryResponse = await documentClient.CreateDocumentQuery<Apprenticeship>(collectionLink, feedOptions)
                        .Where(p => p.ProviderUKPRN == ukprn && (p.RecordStatus == RecordStatus.Live || p.RecordStatus == RecordStatus.MigrationPending))
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Apprenticeship>();

                    apprenticeships.AddRange(queryResponse.ToList());

                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);

                return apprenticeships;
            }

            async Task<Provider> GetExistingProvider(string ukprn, IDocumentClient documentClient)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, ukrlp);

                var query = documentClient
                    .CreateDocumentQuery<Provider>(collectionLink, new FeedOptions()
                    {
                    })
                    .Where(d => d.UnitedKingdomProviderReferenceNumber == ukprn)
                    .AsDocumentQuery();

                return (await query.ExecuteNextAsync()).FirstOrDefault();
            }
        }


    }

    public class ProviderQAStatus
    {
        public int UKPRN { get; set; }
        public bool PassedOverallQAChecks { get; set; }
    }
}

using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data.SqlClient;
using System.Linq;
using Dfc.CourseDirectory.Models.Models.Courses;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Dfc.CourseDirectory.Models.Models.Providers;
using System.IO;
using CsvHelper;
using System.Globalization;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class QAStatusMigrator
    {
        /// <summary>
        /// This function is intended archive venues that are deemed duplicates, and update the the corressponding courses/apprenticeships to reference the
        /// current verion of the venue.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="configuration"></param>
        /// <param name="cosmosDbHelper"></param>
        /// <param name="blobHelper"></param>
        /// <param name="loggerFactory"></param>
        /// <returns></returns>
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
            var coursesCollectionId = "ukrlp";
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


                    // archived venues
                    logCsvWriter.WriteField("UKPRN");
                    logCsvWriter.WriteField("CosmosID");
                    logCsvWriter.WriteField("PassedOverallQAChecks");
                    logCsvWriter.NextRecord();

                    using (var sqlConnection = new SqlConnection(defaultConnectionString))
                    {
                        foreach (var s in qaStatuses)
                        {
                            if (!whitelist.Contains(s.UKPRN))
                            {
                                continue;
                            }

                            var provider = await GetExistingCourse(s.UKPRN.ToString(), cosmosDbClient);
                            if (s.PassedOverallQAChecks)
                            {
                                var sql = @"IF NOT EXISTS (SELECT 1 FROM [Pttcd].[Providers] WHERE ProviderID = @ID) 
                                        BEGIN
                                            INSERT INTO [Pttcd].[Providers] (ProviderId,ApprenticeshipQAStatus) SELECT @ID,@Status
                                        END";
                                var result = sqlConnection.Execute(sql, new
                                {
                                    ID = provider.id,
                                    Status = 16
                                });
                            }

                            // archived venues
                            logCsvWriter.WriteField(s.UKPRN);
                            logCsvWriter.WriteField(provider?.id);
                            logCsvWriter.WriteField(s.PassedOverallQAChecks);
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
                Console.WriteLine(e);
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

            async Task<Provider> GetExistingCourse(string ukprn, DocumentClient documentClient)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);

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

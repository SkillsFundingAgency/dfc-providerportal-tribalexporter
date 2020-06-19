using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Microsoft.Azure.Documents.Linq;
using CsvHelper;
using System.Globalization;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class SetApprenticeshipLocationRadiusToTen
    {
        [FunctionName(nameof(SetApprenticeshipLocationRadiusToTen))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory)
        {
            var cosmosDbClient = cosmosDbHelper.GetClient();
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var apprenticeshipCollectionId = "apprenticeship";
            var whitelistFileName = "ProviderWhiteList.txt";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var whitelist = await GetProviderWhiteList();
            var logFileName = $"SetRadiusToTen-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var apprenticehipsUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var logger = loggerFactory.CreateLogger(typeof(SetApprenticeshipLocationRadiusToTen));

            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {
                logCsvWriter.WriteField("ApprenticeshoId");
                logCsvWriter.WriteField("ApprenticeshipLocationId");
                logCsvWriter.NextRecord();

                try
                {
                    foreach (var ukprn in whitelist)
                    {
                        string continuation = null;
                        do
                        {
                            var feedOptions = new FeedOptions()
                            {
                                RequestContinuation = continuation,
                                PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                            };
                            var queryResponse = await cosmosDbClient.CreateDocumentQuery<Apprenticeship>(apprenticehipsUri, feedOptions)
                                .Where(p => p.ProviderUKPRN == ukprn)
                                .AsDocumentQuery()
                                .ExecuteNextAsync<Apprenticeship>();

                            foreach (var doc in queryResponse)
                            {
                                foreach (var loc in doc.ApprenticeshipLocations)
                                {
                                    if (loc.Radius == 10)
                                    {
                                        loc.Radius = 30;
                                        loc.UpdatedBy = nameof(SetApprenticeshipLocationRadiusToTen);

                                        logCsvWriter.WriteField(doc.id);
                                        logCsvWriter.WriteField(loc.Id);
                                        logCsvWriter.NextRecord();
                                    }
                                }

                                if (doc.ApprenticeshipLocations.Any(x => x.UpdatedBy == nameof(SetApprenticeshipLocationRadiusToTen)))
                                {
                                    doc.UpdatedBy = nameof(SetApprenticeshipLocationRadiusToTen);
                                    var documentLink = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, doc.id.ToString());
                                    await cosmosDbClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions()
                                    {
                                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                                    });
                                }
                            }
                            continuation = queryResponse.ResponseContinuation;
                        } while (continuation != null);
                    }
                } catch (Exception e)
                {
                    logger.LogError(e.Message);
                }

                // Upload log CSV to blob storage
                {
                    logStreamWriter.Flush();

                    logStream.Seek(0L, SeekOrigin.Begin);

                    var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                    await blob.UploadFromStreamAsync(logStream);
                }
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
        }
    }
}

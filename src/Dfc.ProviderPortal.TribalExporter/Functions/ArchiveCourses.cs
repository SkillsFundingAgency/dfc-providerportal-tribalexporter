using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ArchiveCourses
    {
        [FunctionName(nameof(ArchiveCourses))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory)
        {
            var whitelistFileName = "ProviderWhiteList.txt";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var coursesCollectionId = "courses";
            var documentClient = cosmosDbHelper.GetClient();

            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);

            var logger = loggerFactory.CreateLogger(typeof(ArchiveCourses));

            var whitelist = await GetProviderWhiteList();

            foreach (var ukprn in whitelist)
            {
                var updated = 0;

                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    };

                    var queryResponse = await documentClient.CreateDocumentQuery<Course>(coursesCollectionUri, feedOptions)
                        .Where(p => p.ProviderUKPRN == ukprn && p.CourseStatus != CourseDirectory.Models.Enums.RecordStatus.Archived)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Course>();

                    foreach (var doc in queryResponse)
                    {
                        foreach (var run in doc.CourseRuns)
                        {
                            run.RecordStatus = CourseDirectory.Models.Enums.RecordStatus.Archived;
                        }

                        var documentLink = UriFactory.CreateDocumentUri(databaseId, coursesCollectionId, doc.id.ToString());

                        await documentClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions()
                        {
                            PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                        });

                        updated++;
                    }

                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);

                logger.LogInformation($"Archived {updated} courses for {ukprn}");
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

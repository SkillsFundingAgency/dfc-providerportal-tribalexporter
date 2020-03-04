﻿using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Microsoft.Azure.Documents.Linq;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ArchiveApprenticeships
    {
        [FunctionName(nameof(ArchiveApprenticeships))]
        [NoAutomaticTrigger]
        public static async Task Run(
                                        string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                                        [Inject] IConfigurationRoot configuration,
                                        [Inject] ICosmosDbHelper cosmosDbHelper,
                                        [Inject] IBlobStorageHelper blobHelper,
                                        [Inject] ILoggerFactory loggerFactory,
                                        [Inject] IBlobStorageHelper blobhelper)
        {
            var whitelistFileName = "ProviderWhiteList.txt";
            var pendingCoursesFileName = $"CoursesWithNoCostOrCostDecription-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var outputContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var apprenticeshipCollectionId = "apprenticeship";
            var documentClient = cosmosDbHelper.GetClient();
            var result = new List<MigrationPendingCourseRunResult>();

            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var logger = loggerFactory.CreateLogger(typeof(ArchiveCourses));
            int count = 0;
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

                    try
                    {
                        var queryResponse = await documentClient.CreateDocumentQuery<Apprenticeship>(coursesCollectionUri, feedOptions)
                            .Where(p => p.ProviderUKPRN == ukprn && p.CreatedBy != "ApprenticeshipMigrator")
                            .AsDocumentQuery()
                            .ExecuteNextAsync<Apprenticeship>();

                        foreach (var doc in queryResponse)
                        {
                            //mark every location as arhived
                            foreach (var loc in doc.ApprenticeshipLocations)
                            {
                                loc.RecordStatus = CourseDirectory.Models.Enums.RecordStatus.Archived;
                            }

                            //var documentLink = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, doc.id.ToString());

                            //await documentClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions()
                            //{
                            //    PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                            //});

                            updated++;
                            count++;
                        }

                        Console.WriteLine($"updated {updated} Apprenticeships");
                        continuation = queryResponse.ResponseContinuation;
                    }
                    catch (Exception e)
                    {
                        continuation = null;
                        continue;
                    }
                }
                while (continuation != null);
            }

            Console.WriteLine($"Archived {count} Apprenticeships");

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

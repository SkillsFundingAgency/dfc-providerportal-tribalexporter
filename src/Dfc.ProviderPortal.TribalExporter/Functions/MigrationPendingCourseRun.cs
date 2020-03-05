using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Documents.Linq;
using System.IO;
using CsvHelper;
using System.Globalization;
using Dfc.CourseDirectory.Models.Enums;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class MigrationPendingCourseRun
    {
        [FunctionName(nameof(MigrationPendingCourseRun))]
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
            var coursesCollectionId = "courses";
            var documentClient = cosmosDbHelper.GetClient();
            var result = new List<MigrationPendingCourseRunResult>();

            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);
            var logger = loggerFactory.CreateLogger(typeof(ArchiveCourses));
            string continuation = null;
            int count = 0;

            var whitelist = await GetProviderWhiteList();

            foreach (var ukprn in whitelist)
            {
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        EnableCrossPartitionQuery = true,
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    };

                    //find courses that do not have a cost desciption or cost.
                    var queryResponse = await documentClient.CreateDocumentQuery<Course>(coursesCollectionUri, feedOptions)
                        .Where(p => p.CourseRuns.Any(x => x.Cost == null && (x.CostDescription == "" || x.CostDescription == null)) && 
                                    p.CourseStatus == RecordStatus.Live)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Course>();
         
                    //update course run to be migration pending.
                    foreach (var doc in queryResponse)
                    {
                        var currentStatus = doc.CourseStatus;

                        //Course instance id that caused the course to go to migration pending.
                        var message = string.Join("\n", doc.CourseRuns.Where(x => x.Cost == null && (x.CostDescription == "" || x.CostDescription == null))
                                                                      .ToList()
                                                                      .Select(x => $"Course Instance {x.CourseInstanceId} Invalid"));

                        doc.CourseRuns.Where(x => x.Cost == null && (x.CostDescription == "" || x.CostDescription == null))
                                      .ToList()
                                      .ForEach(x => x.RecordStatus = CourseDirectory.Models.Enums.RecordStatus.MigrationPending);

                        var documentLink = UriFactory.CreateDocumentUri(databaseId, coursesCollectionId, doc.id.ToString());
                        await documentClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions()
                        {
                            PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                        });

                        result.Add(new MigrationPendingCourseRunResult { CourseId = doc.CourseId, StatusId = doc.CourseStatus, Message=message });

                        count++;
                    }
                    continuation = queryResponse.ResponseContinuation;
                } while (continuation != null);
            }

            //Log Results to blob storage
            var resultsObjBytes = GetResultAsByteArray(result);
            await WriteResultsToBlobStorage(resultsObjBytes);

            logger.LogInformation($"{count} courses Have been made pending");

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

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(outputContainer, pendingCoursesFileName, data);
            }

            byte[] GetResultAsByteArray(IList<MigrationPendingCourseRunResult> ob)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<MigrationPendingCourseRunResult>(ob);
                        csvWriter.Flush();
                    }

                    return memoryStream.ToArray();
                }
            }
        }
    }

    public class MigrationPendingCourseRunResult
    {
        public int? CourseId { get; set; }
        public RecordStatus StatusId {get;set;}
        public string Message { get; set; }
    }
}

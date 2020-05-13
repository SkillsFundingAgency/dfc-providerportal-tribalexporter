using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ArchiveCoursesWithNoLARSCode
    {
        [FunctionName(nameof(ArchiveCoursesWithNoLARSCode))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory,
            [Inject] IUkrlpApiService ukrlpApiService)
        {
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var coursesCollectionId = "courses";
            var providerCollectionId = "ukrlp";
            var documentClient = cosmosDbHelper.GetClient();
            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);
            var providerCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, providerCollectionId);
            var logger = loggerFactory.CreateLogger(typeof(ArchiveCourses));
            var count = 0;
            var logFileName = $"CoursesWithMissingLarsCodes";

            string continuation = null;
            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {
                // Log CSV headers
                logCsvWriter.WriteField("UKPRN");
                logCsvWriter.WriteField("ProviderName");
                logCsvWriter.WriteField("CourseId");
                logCsvWriter.WriteField("Provider course ID");
                logCsvWriter.WriteField("Course name");
                logCsvWriter.WriteField("Start date");
                logCsvWriter.WriteField("Cost");
                logCsvWriter.WriteField("Cost description");
                logCsvWriter.WriteField("Delivery mode");
                logCsvWriter.WriteField("Attendance mode");
                logCsvWriter.NextRecord();

                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        EnableCrossPartitionQuery = true
                    };

                    var queryResponse = await documentClient.CreateDocumentQuery<Course>(coursesCollectionUri, feedOptions)
                        .Where(p => p.LearnAimRef == null)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Course>();

                    foreach (var doc in queryResponse)
                    {

                        var providers = ukrlpApiService.GetAllProviders(new List<string> { doc.ProviderUKPRN.ToString() });
                        var provider = providers.FirstOrDefault();

                        foreach (var courserun in doc.CourseRuns)
                        {
                            logCsvWriter.WriteField(doc.ProviderUKPRN);
                            logCsvWriter.WriteField(provider?.ProviderName);
                            logCsvWriter.WriteField(courserun.CourseInstanceId);
                            logCsvWriter.WriteField(doc.CourseId);
                            logCsvWriter.WriteField(courserun.CourseName);
                            logCsvWriter.WriteField(courserun.StartDate);
                            logCsvWriter.WriteField(courserun.Cost);
                            logCsvWriter.WriteField(courserun.CostDescription);
                            logCsvWriter.WriteField(courserun.DeliveryMode);
                            logCsvWriter.WriteField(courserun.AttendancePattern);
                            logCsvWriter.NextRecord();

                            courserun.RecordStatus = CourseDirectory.Models.Enums.RecordStatus.Archived;
                            count++;
                        }

                        var documentLink = UriFactory.CreateDocumentUri(databaseId, coursesCollectionId, doc.id.ToString());
                        await documentClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions()
                        {
                            PartitionKey = new Microsoft.Azure.Documents.PartitionKey(doc.ProviderUKPRN)
                        });
                    }

                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);

                // Upload log CSV to blob storage
                {
                    logStreamWriter.Flush();

                    logStream.Seek(0L, SeekOrigin.Begin);

                    var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                    await blob.UploadFromStreamAsync(logStream);
                }
            }

            Console.WriteLine($"{count} courses have been archived");
        }
    }
}

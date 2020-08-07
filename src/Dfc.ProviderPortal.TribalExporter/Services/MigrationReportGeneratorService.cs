using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Reports;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class MigrationReportGeneratorService
    {
        public async Task Run(
                    ILogger log,
                    IConfigurationRoot configuration,
                    ICosmosDbHelper cosmosDbHelper,
                    IBlobStorageHelper blobhelper,
                    IProviderCollectionService providerCollectionService,
                    ICourseCollectionService courseCollectionService,
                    IApprenticeshipCollectionService apprenticeshipCollectionService,
                    IMigrationReportCollectionService migrationReportCollectionService)
        {

            const string AppName = "MigrationReport";
            const string ProviderMigrator_AppName = "Provider.Migrator";
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";

            StringBuilder logFile = new StringBuilder();

            logFile.AppendLine("-------------------------------------------------------");
            logFile.AppendLine("Starting Migration Report generation");
            logFile.AppendLine("-------------------------------------------------------");
            logFile.AppendLine();

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var migrationReportCoursesCollectionId = configuration["CosmosDbCollectionSettings:MigrationReportCoursesCollectionId"];
            var migrationReportApprenticeshipCollectionId = configuration["CosmosDbCollectionSettings:MigrationReportApprenticeshipCollectionId"];
            var migrationReportLogFileName = $"MigrationReport_LogFile-{DateTime.Now.ToString("dd-MM-yy HHmm")}.txt";

            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);

            var whiteListedProviders = await GetProviderWhiteList();

            var _cosmosClient = cosmosDbHelper.GetClient();

            int courseReportEntryCount = 0;
            int appsReportEntryCount = 0;

            var migratedStatusList = new List<RecordStatus>
                {
                    RecordStatus.Live,
                    RecordStatus.MigrationPending,
                };

            // Get all migrated providers
            var migratedProviders = providerCollectionService.GetAllMigratedProviders(ProviderMigrator_AppName).Result;

            // Loop through
            foreach (var ukprn in whiteListedProviders)
             {
                try
                {

                        var provider = await providerCollectionService.GetDocumentByUkprn(ukprn);

                        logFile.AppendLine($"STARTED : Generating report for provide. {provider.UnitedKingdomProviderReferenceNumber}");

                        // deal with Apprenticeship, create one entry if provider = both or apprenticeship
                        if (provider.ProviderType == CourseDirectory.Models.Models.Providers.ProviderType.Both
                            || provider.ProviderType == CourseDirectory.Models.Models.Providers.ProviderType.Apprenticeship)
                        {
                            // Get Apprenticeships
                            var apprenticeships = await apprenticeshipCollectionService.GetAllApprenticeshipsByUkprnAsync(provider.UnitedKingdomProviderReferenceNumber);

                            // Get App report for the provider
                            MigrationReportEntry appReportEntry = await migrationReportCollectionService.GetReportForApprenticeshipByUkprn(ukprn);
                            if (appReportEntry == null)
                            {
                                // Check if exists if not create one
                                appReportEntry = new MigrationReportEntry();
                            }

                            // prep report
                            appReportEntry.CreatedOn = DateTime.Now;
                            appReportEntry.CreatedBy = AppName;
                            appReportEntry.id = provider.UnitedKingdomProviderReferenceNumber;

                            appReportEntry.MigrationPendingCount = apprenticeships.Where(a => a.RecordStatus == RecordStatus.MigrationPending).Count();
                            appReportEntry.MigrationReadyToGoLive = apprenticeships.Where(a => a.RecordStatus == RecordStatus.MigrationReadyToGoLive).Count();
                            appReportEntry.BulkUploadPendingcount = apprenticeships.Where(a => a.RecordStatus == RecordStatus.BulkUloadPending).Count();
                            appReportEntry.BulkUploadReadyToGoLiveCount = apprenticeships.Where(a => a.RecordStatus == RecordStatus.BulkUploadReadyToGoLive).Count();
                            appReportEntry.LiveCount = apprenticeships.Where(cr => cr.RecordStatus == RecordStatus.Live).Count();
                            appReportEntry.PendingCount = apprenticeships.Where(cr => cr.RecordStatus == RecordStatus.Pending).Count();
                            appReportEntry.MigratedCount = apprenticeships.Where(cr => migratedStatusList.Contains(cr.RecordStatus)).Count(); //everthing that came across.

                            appReportEntry.MigrationDate = provider.DateUpdated;
                            appReportEntry.ProviderType = (int)provider.ProviderType;
                            appReportEntry.ProviderName = provider.ProviderName;
                            appReportEntry.FailedMigrationCount = 0; // Can't determine this so leave as zero
                            appReportEntry.MigrationRate = ApprenticeshipMigrationRate(apprenticeships);

                            // Update reports collection for apps
                            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, migrationReportApprenticeshipCollectionId);
                            await _cosmosClient.UpsertDocumentAsync(collectionUri, appReportEntry);

                            appsReportEntryCount++;

                        }

                        // Deal with courses, create one entry for course only
                        if (provider.ProviderType == CourseDirectory.Models.Models.Providers.ProviderType.Both)
                        {
                            MigrationReportEntry courseReportEntry = await migrationReportCollectionService.GetReportForCoursesByUkprn(ukprn);
                            if (courseReportEntry == null)
                            {
                                // Check if exists if not create one
                                courseReportEntry = new MigrationReportEntry();
                            }

                            // Get Courses for provider
                            var courses = await courseCollectionService.GetAllCoursesByUkprnAsync(ukprn);

                            courseReportEntry.CreatedOn = DateTime.Now;
                            courseReportEntry.CreatedBy = AppName;
                            courseReportEntry.id = provider.UnitedKingdomProviderReferenceNumber;

                            courseReportEntry.MigrationPendingCount = courses.SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.MigrationPending)).Count();
                            courseReportEntry.MigrationReadyToGoLive = courses.SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.MigrationReadyToGoLive)).Count();
                            courseReportEntry.BulkUploadPendingcount = courses.SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.BulkUloadPending)).Count();
                            courseReportEntry.BulkUploadReadyToGoLiveCount = courses.SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.BulkUploadReadyToGoLive)).Count();
                            courseReportEntry.LiveCount = courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Live)).Count();
                            courseReportEntry.PendingCount = courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Pending)).Count();
                            courseReportEntry.MigratedCount = courses.SelectMany(c => c.CourseRuns.Where(cr => migratedStatusList.Contains(cr.RecordStatus))).Count();

                            courseReportEntry.MigrationDate = provider.DateUpdated;
                            courseReportEntry.ProviderType = (int)provider.ProviderType;
                            courseReportEntry.ProviderName = provider.ProviderName;
                            courseReportEntry.FailedMigrationCount = 0; // Can't determine this so leave as zero
                            courseReportEntry.MigrationRate = CourseMigrationRate(courses);

                            // Update reports collection for courses
                            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, migrationReportCoursesCollectionId);
                            await _cosmosClient.UpsertDocumentAsync(collectionUri, courseReportEntry);

                            courseReportEntryCount++;
                        }

                        logFile.AppendLine($"COMPLETED : Report for provider {provider.UnitedKingdomProviderReferenceNumber}");
                }
                catch (Exception ex)
                {
                    logFile.AppendLine($"Error creating report for {ukprn}. {ex.GetBaseException().Message}");
                }
            }

            stopWatch.Stop();
            logFile.AppendLine("----------------------------------------------------------------");
            logFile.AppendLine($"Completed Migration Report generation in {stopWatch.Elapsed.TotalMinutes} minutes.");
            logFile.AppendLine($"Course Report Entries :  {courseReportEntryCount} for {migratedProviders.Count} migrated providers.");
            logFile.AppendLine($"Apps Report Entries :  {appsReportEntryCount} for {migratedProviders.Count} migrated providers");
            logFile.AppendLine("----------------------------------------------------------------");

            // Write log to blob for debug.
            var resultsObjBytes = GetResultAsByteArray(logFile);
            await WriteResultsToBlobStorage(resultsObjBytes);

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, migrationReportLogFileName, data);
            }

            async Task<IList<int>> GetProviderWhiteList()
            {
                var list = new List<int>();
                var whiteList = await blobhelper.ReadFileAsync(blobContainer, WHITE_LIST_FILE);
                if (!string.IsNullOrEmpty(whiteList))
                {
                    var lines = whiteList.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (string line in lines)
                    {
                        if (int.TryParse(line, out int id))
                        {
                            list.Add(id);
                        }
                    }
                }
                return list;
            }

            byte[] GetResultAsByteArray(StringBuilder logData)
            {
                using (var memoryStream = new MemoryStream())
                using (var writer = new StreamWriter(memoryStream))
                {
                    // Various for loops etc as necessary that will ultimately do this:
                    writer.Write(logData.ToString());
                    writer.Flush();
                    return memoryStream.ToArray();
                }
            }

            decimal CourseMigrationRate(IList<Course> courses)
            {
                var liveCourses = (decimal)courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Live)).Count();
                var migratedDataValue = ((decimal)courses.SelectMany(c => c.CourseRuns.Where(cr => migratedStatusList.Contains(cr.RecordStatus))).Count());

                return ((liveCourses / migratedDataValue) * 100);
            }

            decimal ApprenticeshipMigrationRate(IList<Apprenticeship> apprenticeships)
            {
                    var liveApprenticeship = (decimal)apprenticeships.Where(cr => cr.RecordStatus == RecordStatus.Live).Count();
                    var migratedDataValue = (decimal)apprenticeships.Where(cr => migratedStatusList.Contains(cr.RecordStatus)).Count();

                    return ((liveApprenticeship / migratedDataValue) * 100);
            }
        }
    }
}

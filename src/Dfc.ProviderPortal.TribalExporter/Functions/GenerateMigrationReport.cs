using Dfc.CourseDirectory.Models.Models.Reports;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Microsoft.Azure.Documents.Client;
using System.IO;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class GenerateMigrationReport
    {
        [FunctionName(nameof(GenerateMigrationReport))]
        //[NoAutomaticTrigger]
        public static async Task Run(
                    //string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    [TimerTrigger("%MigrationReportSchedule%")]TimerInfo myTimer,
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICourseCollectionService courseCollectionService,
                    [Inject] IApprenticeshipCollectionService apprenticeshipCollectionService,
                    [Inject] IMigrationReportCollectionService migrationReportCollectionService)
        {
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";
            const string AppName = "MigrationReport";

            const string ProviderMigrator_AppName = "Provider.Migrator";

            StringBuilder logFile = new StringBuilder();

            logFile.AppendLine("-------------------------------------------------------");
            logFile.AppendLine("Starting Migration Report generation");
            logFile.AppendLine("-------------------------------------------------------");
            logFile.AppendLine();

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // TODO : Change to correct collection below
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var migrationReportCoursesCollectionId = configuration["CosmosDbCollectionSettings:MigrationReportCoursesCollectionId"];
            var migrationReportApprenticeshipCollectionId = configuration["CosmosDbCollectionSettings:MigrationReportApprenticeshipCollectionId"];
            var migrationReportLogFileName = $"MigrationReport_LogFile-{DateTime.Now.ToString("dd-MM-yy HHmm")}.txt";

            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);

            var _cosmosClient = cosmosDbHelper.GetClient();

            var whiteListProviders = await GetProviderWhiteList();
            List<string> migratedProviders = new List<string>();
            List<string> notMigratedProviders = new List<string>();
            int courseReportEntryCount = 0;
            int appsReportEntryCount = 0;

            var migratedStatusList = new List<RecordStatus>
                {
                    RecordStatus.Live,
                    RecordStatus.MigrationPending,
                };

            // Loop through whitelist
            foreach (var ukprn in whiteListProviders)
            {

                // Get provider 
                var provider = await providerCollectionService.GetDocumentByUkprn(ukprn);

                try
                {
                    // Update only if migrated via the Migration process
                    if (provider.LastUpdatedBy == ProviderMigrator_AppName)
                    {
                        logFile.AppendLine($"STARTED : Generating report for provider {ukprn}"); 

                        // add to counter
                        migratedProviders.Add(provider.UnitedKingdomProviderReferenceNumber);

                        // deal with Apprenticeship, create one entry if provider = both or apprenticeship
                        if (provider.ProviderType == CourseDirectory.Models.Models.Providers.ProviderType.Both
                            || provider.ProviderType == CourseDirectory.Models.Models.Providers.ProviderType.Apprenticeship)
                        {
                            // Get Apprenticeships
                            var apprenticeships = await apprenticeshipCollectionService.GetAllApprenticeshipsByUkprnAsync(ukprn);

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

                            var apprenticeshipLocations = apprenticeships.Where(a => a.ApprenticeshipLocations != null).SelectMany(l => l.ApprenticeshipLocations);
                            appReportEntry.MigrationPendingCount = apprenticeshipLocations.Where(a => a.RecordStatus == RecordStatus.MigrationPending).Count();
                            appReportEntry.MigrationReadyToGoLive = apprenticeshipLocations.Where(a => a.RecordStatus == RecordStatus.MigrationReadyToGoLive).Count();
                            appReportEntry.BulkUploadPendingcount = apprenticeshipLocations.Where(a => a.RecordStatus == RecordStatus.BulkUloadPending).Count();
                            appReportEntry.BulkUploadReadyToGoLiveCount = apprenticeshipLocations.Where(a => a.RecordStatus == RecordStatus.BulkUploadReadyToGoLive).Count();
                            appReportEntry.LiveCount = apprenticeshipLocations.Where(cr => cr.RecordStatus == RecordStatus.Live).Count();
                            appReportEntry.PendingCount = apprenticeshipLocations.Where(cr => cr.RecordStatus == RecordStatus.Pending).Count();
                            appReportEntry.MigratedCount = apprenticeshipLocations.Where(cr => migratedStatusList.Contains(cr.RecordStatus)).Count(); //everthing that came across.

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

                        logFile.AppendLine($"COMPLETED : Report for provider {ukprn}");

                    }
                    else
                    {
                        logFile.AppendLine($"SKIPPED : Not part of migration {ukprn}");
                        notMigratedProviders.Add(provider.UnitedKingdomProviderReferenceNumber);
                    }
                }
                catch (Exception ex)
                {
                    logFile.AppendLine($"Error creating report for {ukprn}, {provider.ProviderType}. {ex.GetBaseException().Message}");
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

            // Get list of providers to migrate
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

            decimal CourseMigrationRate(IList<Course> courses)
            {
                if (courses.SelectMany(c => c.CourseRuns.Where(cr => migratedStatusList.Contains(cr.RecordStatus))).Any())
                {

                    var liveCourses = (decimal)courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Live)).Count();
                    var migratedDataValue = ((decimal)courses.SelectMany(c => c.CourseRuns.Where(cr => migratedStatusList.Contains(cr.RecordStatus))).Count());

                    return ((liveCourses / migratedDataValue) * 100);
                }

                return 0;
            }

            decimal ApprenticeshipMigrationRate(IList<Apprenticeship> apprenticeships)
            {
                var locations = apprenticeships.Where(a => a.ApprenticeshipLocations != null).SelectMany(l => l.ApprenticeshipLocations);

                if (locations.Where(cr => migratedStatusList.Contains(cr.RecordStatus)).Any())
                {
                    var liveApprenticeship = (decimal)locations.Where(cr => cr.RecordStatus == RecordStatus.Live).Count();
                    var migratedDataValue = (decimal)locations.Where(cr => migratedStatusList.Contains(cr.RecordStatus)).Count();

                    return ((liveApprenticeship / migratedDataValue) * 100);
                }

                return 0;
            }
        }
    }
}

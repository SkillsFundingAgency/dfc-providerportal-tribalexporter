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
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.CourseDirectory.Models.Models.Reports;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class MigrationReportGeneratorService
    {
        private const string AppName = "MigrationReport";

        public async Task Run(
                    ILogger log,
                    IConfigurationRoot configuration,
                    ICosmosDbHelper cosmosDbHelper,
                    IBlobStorageHelper blobHelper,
                    IProviderCollectionService providerCollectionService,
                    ICourseCollectionService courseCollectionService,
                    IApprenticeshipCollectionService apprenticeshipCollectionService,
                    IMigrationReportCollectionService migrationReportCollectionService)
        {

            log.LogInformation("Starting Migration Report generation");

            var migrationLog = new StringBuilder();
            migrationLog.AppendLine("-------------------------------------------------------");
            migrationLog.AppendLine("Starting Migration Report generation");
            migrationLog.AppendLine("-------------------------------------------------------");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            IReadOnlyList<RecordStatus> migratedStatusList = new List<RecordStatus>
            {
                RecordStatus.Live,
                RecordStatus.MigrationPending,
            };

            var blobContainer = blobHelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);

            var whiteListedProviders = await GetProviderWhiteList(blobHelper, blobContainer);
            var cosmosClient = cosmosDbHelper.GetClient();

            log.LogDebug("Fetching migrated provider count...");
            var migratedProvidersCount = (await providerCollectionService.GetAllMigratedProviders("Provider.Migrator")).Count;
            log.LogDebug($"Migrated Provider count: {migratedProvidersCount}.");

            log.LogDebug("Fetching providers...");
            var providers = await providerCollectionService.GetDocumentsByUkprn(whiteListedProviders);

            var providerTypeCounts = providers.GroupBy(t => t.ProviderType).Select(g => new {type = g.Key, qty = g.Count()});
            log.LogDebug($"Provider counts: {string.Join("; ", providerTypeCounts.Select(c => $"{c.type}: {c.qty}"))}. Total: {providers.Count}");

            int progress = 1;
            int feCourseReportEntryCount = 0;
            int apprenticeshipsReportEntryCount = 0;
            foreach (var ukprn in whiteListedProviders)
            {
                try
                {
                    var provider = providers.Single(p => int.Parse(p.UnitedKingdomProviderReferenceNumber) == ukprn);

                    var logStart = $"STARTED : Generating report for provider with UKPRN: {provider.UnitedKingdomProviderReferenceNumber}. Progress: {progress++}/{whiteListedProviders.Count}";
                    log.LogDebug(logStart);
                    migrationLog.AppendLine(logStart);

                    switch (provider.ProviderType)
                    {
                        case ProviderType.Both:
                            await GenerateApprenticeshipReport(
                                configuration, apprenticeshipCollectionService, migrationReportCollectionService, provider,
                                migratedStatusList, cosmosClient);
                            apprenticeshipsReportEntryCount++;
                            await GenerateFECourseReport(
                                configuration, courseCollectionService, migrationReportCollectionService, provider,
                                migratedStatusList, cosmosClient);
                            feCourseReportEntryCount++;
                            break;
                        case ProviderType.Apprenticeship:
                            await GenerateApprenticeshipReport(
                                configuration, apprenticeshipCollectionService, migrationReportCollectionService, provider,
                                migratedStatusList, cosmosClient);
                            apprenticeshipsReportEntryCount++;
                            break;
                        case ProviderType.FE:
                            await GenerateFECourseReport(
                                configuration, courseCollectionService, migrationReportCollectionService, provider,
                                migratedStatusList, cosmosClient);
                            feCourseReportEntryCount++;
                            break;
                        case ProviderType.Undefined:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    migrationLog.AppendLine($"COMPLETED : Report for provider {provider.UnitedKingdomProviderReferenceNumber}");
                }
                catch (Exception ex)
                {
                    migrationLog.AppendLine($"Error creating report for {ukprn}. {ex.GetBaseException().Message}");
                    log.LogError(ex, $"Error creating report for {ukprn}.");
                }
            }

            stopWatch.Stop();
            migrationLog.AppendLine("----------------------------------------------------------------");
            migrationLog.AppendLine($"Completed Migration Report generation in {stopWatch.Elapsed.TotalMinutes} minutes.");
            migrationLog.AppendLine($"Course Report Entries :  {feCourseReportEntryCount} for {migratedProvidersCount} migrated providers.");
            migrationLog.AppendLine($"Apps Report Entries :  {apprenticeshipsReportEntryCount} for {migratedProvidersCount} migrated providers");
            migrationLog.AppendLine("----------------------------------------------------------------");
            log.LogDebug(migrationLog.ToString());

            await blobHelper.UploadFile(
                blobContainer,
                $"MigrationReport_LogFile-{DateTime.Now:dd-MM-yy HHmm}.txt",
                GetResultAsByteArray(migrationLog));

            log.LogInformation($"Completed Migration Report generation. {feCourseReportEntryCount + apprenticeshipsReportEntryCount} records processed.");
        }

        private static async Task GenerateFECourseReport(
            IConfiguration configuration,
            ICourseCollectionService courseCollectionService,
            IMigrationReportCollectionService migrationReportCollectionService,
            Provider provider,
            IReadOnlyList<RecordStatus> migratedStatusList,
            IDocumentClient cosmosClient)
        {
            var ukprn = int.Parse(provider.UnitedKingdomProviderReferenceNumber);
            var courseReportEntry = await migrationReportCollectionService.GetReportForCoursesByUkprn(ukprn) ??
                                    new MigrationReportEntry();

            var courses = await courseCollectionService.GetAllCoursesByUkprnAsync(ukprn);

            courseReportEntry.CreatedOn = DateTime.Now;
            courseReportEntry.CreatedBy = AppName;
            courseReportEntry.id = provider.UnitedKingdomProviderReferenceNumber;
            courseReportEntry.MigrationDate = provider.DateUpdated;
            courseReportEntry.ProviderType = (int)provider.ProviderType;
            courseReportEntry.ProviderName = provider.ProviderName;
            courseReportEntry.FailedMigrationCount = 0;
            courseReportEntry.MigrationRate = FeCourseMigrationRate(courses, migratedStatusList);

            courseReportEntry.MigrationPendingCount = courses
                .SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.MigrationPending)).Count();

            courseReportEntry.MigrationReadyToGoLive = courses
                .SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.MigrationReadyToGoLive))
                .Count();

            courseReportEntry.BulkUploadPendingcount = courses
                .SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.BulkUloadPending)).Count();

            courseReportEntry.BulkUploadReadyToGoLiveCount = courses
                .SelectMany(x => x.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.BulkUploadReadyToGoLive))
                .Count();

            courseReportEntry.LiveCount =
                courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Live))
                    .Count();

            courseReportEntry.PendingCount =
                courses.SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Pending))
                    .Count();

            courseReportEntry.MigratedCount = courses
                .SelectMany(c => c.CourseRuns.Where(cr => migratedStatusList.Contains(cr.RecordStatus)))
                .Count();


            var collectionUri = UriFactory.CreateDocumentCollectionUri(
                configuration["CosmosDbSettings:DatabaseId"],
                configuration["CosmosDbCollectionSettings:MigrationReportCoursesCollectionId"]);

            await cosmosClient.UpsertDocumentAsync(collectionUri, courseReportEntry);
        }

        private static async Task GenerateApprenticeshipReport(
            IConfiguration configuration,
            IApprenticeshipCollectionService apprenticeshipCollectionService,
            IMigrationReportCollectionService migrationReportCollectionService,
            Provider provider,
            IReadOnlyList<RecordStatus> migratedStatusList,
            IDocumentClient cosmosClient)
        {
            var apprenticeships =
                await apprenticeshipCollectionService.GetAllApprenticeshipsByUkprnAsync(provider
                    .UnitedKingdomProviderReferenceNumber);
            var appReportEntry =
                await migrationReportCollectionService.GetReportForApprenticeshipByUkprn(
                    int.Parse(provider.UnitedKingdomProviderReferenceNumber)) ?? new MigrationReportEntry();

            appReportEntry.CreatedOn = DateTime.Now;
            appReportEntry.CreatedBy = AppName;
            appReportEntry.id = provider.UnitedKingdomProviderReferenceNumber;
            appReportEntry.MigrationPendingCount = apprenticeships.Count(a => a.RecordStatus == RecordStatus.MigrationPending);
            appReportEntry.MigrationReadyToGoLive = apprenticeships.Count(a => a.RecordStatus == RecordStatus.MigrationReadyToGoLive);
            appReportEntry.BulkUploadPendingcount = apprenticeships.Count(a => a.RecordStatus == RecordStatus.BulkUloadPending);
            appReportEntry.BulkUploadReadyToGoLiveCount = apprenticeships.Count(a => a.RecordStatus == RecordStatus.BulkUploadReadyToGoLive);
            appReportEntry.LiveCount = apprenticeships.Count(cr => cr.RecordStatus == RecordStatus.Live);
            appReportEntry.PendingCount = apprenticeships.Count(cr => cr.RecordStatus == RecordStatus.Pending);
            appReportEntry.MigratedCount = apprenticeships.Count(cr => migratedStatusList.Contains(cr.RecordStatus));
            appReportEntry.MigrationDate = provider.DateUpdated;
            appReportEntry.ProviderType = (int)provider.ProviderType;
            appReportEntry.ProviderName = provider.ProviderName;
            appReportEntry.FailedMigrationCount = 0;
            appReportEntry.MigrationRate = ApprenticeshipMigrationRate(apprenticeships, migratedStatusList);

            var collectionUri = UriFactory.CreateDocumentCollectionUri(configuration["CosmosDbSettings:DatabaseId"],
                configuration["CosmosDbCollectionSettings:MigrationReportApprenticeshipCollectionId"]);
            await cosmosClient.UpsertDocumentAsync(collectionUri, appReportEntry);
        }

        private static async Task<IList<int>> GetProviderWhiteList(IBlobStorageHelper blobStorageHelper, CloudBlobContainer cloudBlobContainer)
        {
            var whiteList = await blobStorageHelper.ReadFileAsync(cloudBlobContainer, "ProviderWhiteList.txt");
            if (string.IsNullOrEmpty(whiteList))
            {
                return new List<int>();
            }

            var list = new List<int>();
            foreach (var line in whiteList.Split(new[] {Environment.NewLine}, StringSplitOptions.None))
            {
                if (int.TryParse(line, out int id))
                {
                    list.Add(id);
                }
            }

            return list;
        }

        private static byte[] GetResultAsByteArray(StringBuilder logData)
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

        private static decimal FeCourseMigrationRate(IList<Course> courses, IReadOnlyList<RecordStatus> recordStatuses)
        {
            var liveCourses = (decimal)courses .SelectMany(c => c.CourseRuns.Where(cr => cr.RecordStatus == RecordStatus.Live)).Count();
            var migratedDataValue = ((decimal)courses .SelectMany(c => c.CourseRuns.Where(cr => recordStatuses.Contains(cr.RecordStatus))).Count());
            if (migratedDataValue == 0)
            {
                return 0;
            }

            return liveCourses / migratedDataValue * 100;
        }

        private static decimal ApprenticeshipMigrationRate(IList<Apprenticeship> apprenticeships, IReadOnlyList<RecordStatus> recordStatuses)
        {
            var liveApprenticeship = (decimal)apprenticeships.Count(cr => cr.RecordStatus == RecordStatus.Live);
            var migratedDataValue = (decimal)apprenticeships.Count(cr => recordStatuses.Contains(cr.RecordStatus));

            if (migratedDataValue == 0)
            {
                return 0;
            }

            return liveApprenticeship / migratedDataValue * 100;
        }
    }
}

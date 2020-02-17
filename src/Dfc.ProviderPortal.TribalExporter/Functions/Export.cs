using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Common.Settings;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Serialization;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class Export
    {
        [FunctionName(nameof(Export))]
        public static async Task Run(
            [TimerTrigger("%schedule%")]TimerInfo myTimer,
            ILogger log,
            [Inject] IOptions<ExporterSettings> exporterSettings,
            [Inject] IBlobStorageHelper blobStorageHelper,
            [Inject] IProviderCollectionService providerCollectionService,
            [Inject] ICourseCollectionService courseCollectionService,
            [Inject] IVenueCollectionService venueCollectionService)
        {
                        var configuration = exporterSettings.Value;

            log.LogInformation("[Export] waiting for trigger...");

            //TODO: add more logging after you get this working ...
            var logFile = new StringBuilder();
            logFile.AppendLine($"Starting {nameof(Export)} at {DateTime.Now}");

            var fileNames = new List<string>();
            var startDate = configuration.ExporterStartDate;

            var providersFileName = $"{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Providers_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

            logFile.AppendLine($"Start date: {startDate:dd/MM/yyyy hh:mm}");
            logFile.AppendLine($"Provider filename: {providersFileName}");

            var containerNameExporter = configuration.ContainerNameExporter;
            var containerNameProviderFiles = configuration.ContainerNameProviderFiles;
            var migrationProviderCsv = configuration.MigrationProviderCsv;

            logFile.AppendLine($"Attempting to get reference to blob containers: {containerNameExporter}, {containerNameProviderFiles}");

            var containerExporter = blobStorageHelper.GetBlobContainer(containerNameExporter);
            var containerProviderFiles = blobStorageHelper.GetBlobContainer(containerNameProviderFiles);

            logFile.AppendLine($"Got references to blob containers: {containerNameExporter}, {containerNameProviderFiles}");

            try
            {
                log.LogInformation("[Export] grabbing providers");
                var providersForExport = await GetProvidersFromCsv(migrationProviderCsv, blobStorageHelper, logFile, containerProviderFiles);

                var providerFileName = await GenerateProvidersExport(providerCollectionService, providersForExport, logFile, providersFileName, containerExporter, containerNameExporter);
                fileNames.Add(providerFileName);

                var count = 0;
                var total = providersForExport.Count();

                // N.B. Deliberately not doing these in parallel to avoid creating too many DocumentClients...
                foreach (var provider in providersForExport)
                {
                    count++;

                    log.LogInformation($"[Export] checking {provider.Ukprn} [{count} of {total}]");

                    var export = await CheckForProviderUpdates(log, courseCollectionService,
                            venueCollectionService, logFile, provider, startDate, containerExporter,
                            containerNameExporter)
                        .ConfigureAwait(false);

                    fileNames.AddRange(export);
                }

                var fileNamesFileName = $"{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\FileNames.json";
                var fileNamesBlob = containerExporter.GetBlockBlobReference(fileNamesFileName);
                await fileNamesBlob.UploadTextAsync(JsonConvert.SerializeObject(fileNames, Formatting.Indented));
            }
            catch (Exception e)
            {
                logFile.AppendLine(e.Message);
                logFile.AppendLine(e.ToString());
                throw;
            }
            finally
            {
                logFile.AppendLine($"Ending {nameof(Export)} at {DateTime.Now}");
                var logFileName = $"{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Log_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.txt";
                var logFileNameBlob = containerExporter.GetBlockBlobReference(logFileName);
                await logFileNameBlob.UploadTextAsync(logFile.ToString());
            }
        }

        private static async Task<IEnumerable<IMiragtionProviderItem>> GetProvidersFromCsv(
            string filename, 
            IBlobStorageHelper blobStorageHelper,
            StringBuilder logFile, 
            CloudBlobContainer containerProviderFiles)
        {
            var migrationProviderCsv = filename;

            logFile.AppendLine($"Attempting to get content from Migration Provider CSV file: {migrationProviderCsv}");

            var migrationProvidersCsvContent =
                await blobStorageHelper.ReadFileAsync(containerProviderFiles, migrationProviderCsv);

            logFile.AppendLine(
                $"Got content from Migration Provider CSV file: {migrationProviderCsv} [content length: {migrationProvidersCsvContent.Length}]");

            logFile.AppendLine(migrationProviderCsv);

            logFile.AppendLine(migrationProvidersCsvContent);

            logFile.AppendLine($"Attempting to deserialise content into a IEnumerable<{nameof(MiragtionProviderItem)}>");

            var mpItems = MigrationProviderItemHelper.GetMiragtionProviderItems(migrationProvidersCsvContent, logFile);

            logFile.AppendLine(
                $"Start of deserialised content into: {mpItems.Count()} items in IEnumerable<{nameof(MiragtionProviderItem)}>");

            logFile.Append(string.Join("," + Environment.NewLine, mpItems));

            logFile.AppendLine($"{Environment.NewLine}End of deserialised content");
            return mpItems;
        }

        private static async Task<string> GenerateProvidersExport(
            IProviderCollectionService providerCollectionService,
            IEnumerable<IMiragtionProviderItem> mpItems, 
            StringBuilder logFile, 
            string providersFileName, 
            CloudBlobContainer containerExporter,
            string containerNameExporter)
        {
            var ukprns = mpItems.AsUkprns();

            if (ukprns != null && ukprns.Any())
            {
                logFile.AppendLine($"Attempting to get providers' data for UKPRNS: {string.Join(",", ukprns)}");

                var providers = await providerCollectionService.GetAllAsJsonAsync(ukprns);

                logFile.AppendLine($"Got all the providers' data.");

                logFile.AppendLine($"Attempting to get reference to block blob containers for file: {providersFileName}");

                var providersBlob = containerExporter.GetBlockBlobReference(providersFileName);

                logFile.AppendLine($"Got reference to block blob containers for file: {providersFileName}");

                logFile.AppendLine($"Attempting to upload file {providersFileName} to blob container {containerNameExporter}");

                await providersBlob.UploadTextAsync(providers);

                logFile.AppendLine($"Uploaded file {providersFileName} to blob container {containerNameExporter}");

                return providersFileName;
            }

            return null;
        }

        private static async Task<IEnumerable<string>> CheckForProviderUpdates(
            ILogger log,
            ICourseCollectionService courseCollectionService,
            IVenueCollectionService venueCollectionService, 
            StringBuilder logFile, 
            IMiragtionProviderItem mpItem,
            DateTime fromDate, 
            CloudBlobContainer containerExporter, 
            string containerNameExporter)
        {
            var fileNames = new List<string>();

            logFile.AppendLine($"Attempting to get conditional data for: {mpItem}");

            var hasTodaysDate = mpItem.DateMigrated.Date == DateTime.Today;
            var dateMigratedIsInThePast = mpItem.DateMigrated.Date < DateTime.Today;

            var hasCreatedCourses = await courseCollectionService.HasCoursesBeenCreatedSinceAsync(mpItem.Ukprn, fromDate);
            var hasCreatedCourseRuns =
                await courseCollectionService.HasCourseRunsBeenCreatedSinceAsync(mpItem.Ukprn, fromDate);

            var hasUpdatedCourses = await courseCollectionService.HasCoursesBeenUpdatedSinceAsync(mpItem.Ukprn, fromDate);
            var hasUpdatedCourseRuns =
                await courseCollectionService.HasCourseRunsBeenUpdatedSinceAsync(mpItem.Ukprn, fromDate);

            var hasDeletedCourses = await courseCollectionService.HasCoursesBeenDeletedSinceAsync(mpItem.Ukprn, fromDate);
            var hasDeletedCourseRuns =
                await courseCollectionService.HasCourseRunsBeenDeletedSinceAsync(mpItem.Ukprn, fromDate);

            var hasUpdatedVenues = await venueCollectionService.HasBeenAnUpdatedSinceAsync(mpItem.Ukprn, fromDate);

            logFile.AppendLine($"Got conditional data for: {mpItem}");
            logFile.AppendLine($"\tHas today's date: {hasTodaysDate}");
            logFile.AppendLine($"\tDate migrated is in the past: {dateMigratedIsInThePast}");
            logFile.AppendLine($"\tHas created Courses: {hasCreatedCourses}");
            logFile.AppendLine($"\tHas created CourseRuns: {hasCreatedCourseRuns}");
            logFile.AppendLine($"\tHas updated Courses: {hasUpdatedCourses}");
            logFile.AppendLine($"\tHas updated CourseRuns: {hasUpdatedCourseRuns}");
            logFile.AppendLine($"\tHas deleted Courses: {hasDeletedCourses}");
            logFile.AppendLine($"\tHas deleted CourseRuns: {hasDeletedCourseRuns}");
            logFile.AppendLine($"\tHas updated Venues: {hasUpdatedVenues}");
            logFile.AppendLine($"End of conditional data for: {mpItem}");

            if (hasTodaysDate || (dateMigratedIsInThePast))
            {
                if (hasCreatedCourses || hasCreatedCourseRuns || hasUpdatedCourses || hasUpdatedCourseRuns ||
                    hasDeletedCourses || hasDeletedCourseRuns)
                {
                    log.LogInformation($"updating courses for {mpItem.Ukprn}");

                    var courseFilename = await GenerateCoursesExportForProvider(log, courseCollectionService, logFile, mpItem, containerExporter,
                        containerNameExporter).ConfigureAwait(false);

                    fileNames.Add(courseFilename);
                }

                if (hasUpdatedVenues)
                {
                    log.LogInformation($"updating venues for {mpItem.Ukprn}");

                    var venueFilename = await GenerateVenuesExportForProvider(log, venueCollectionService, logFile, mpItem, containerExporter,
                        containerNameExporter).ConfigureAwait(false);

                    fileNames.Add(venueFilename);
                }
            }
            else
            {
                logFile.AppendLine($"Conditional logic for {mpItem} is False.");
            }

            return fileNames;
        }

        private static async Task<string> GenerateVenuesExportForProvider(
            ILogger log,
            IVenueCollectionService venueCollectionService,
            StringBuilder logFile, 
            IMiragtionProviderItem mpItem, 
            CloudBlobContainer containerExporter,
            string containerNameExporter)
        {
            logFile.AppendLine($"\tAttempting to get venues' data for: {mpItem}");

            var venues = await venueCollectionService.GetAllVenuesAsJsonForUkprnAsync(mpItem.Ukprn);

            logFile.AppendLine($"\tGot venues' data for: {mpItem}");

            if (venues != "[]")
            {
                logFile.AppendLine($"\t\tHas venues' data for: {mpItem}");

                var venuesFileName =
                    $"{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Venues_for_Provider_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                logFile.AppendLine($"\t\tGot reference to block blob containers for file: {venuesFileName}");

                var venuesBlob = containerExporter.GetBlockBlobReference(venuesFileName);

                logFile.AppendLine($"\t\tAttempting to upload file {venuesFileName} to blob container {containerNameExporter}");

                await venuesBlob.UploadTextAsync(venues);

                log.LogInformation($"uploaded {venuesFileName}");

                logFile.AppendLine($"\t\tUploaded file {venuesFileName} to blob container {containerNameExporter}");

                return venuesFileName;
            }
            else
            {
                logFile.AppendLine($"\t\tHas no venues' data for: {mpItem}");
                return null;
            }
        }

        private static async Task<string> GenerateCoursesExportForProvider(
            ILogger log,
            ICourseCollectionService courseCollectionService,
            StringBuilder logFile, 
            IMiragtionProviderItem mpItem, 
            CloudBlobContainer containerExporter,
            string containerNameExporter)
        {
            logFile.AppendLine($"Conditional logic for {mpItem} is True.");

            logFile.AppendLine($"\tAttempting to get courses' data for: {mpItem}");

            var courses = await courseCollectionService.GetAllLiveCoursesAsJsonForUkprnAsync(mpItem.Ukprn);

            logFile.AppendLine($"\tGot courses' data for: {mpItem}");

            if (courses != "[]")
            {
                logFile.AppendLine($"\t\tHas courses' data for: {mpItem}");

                var coursesFileName =
                    $"{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Courses_for_Provider_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                logFile.AppendLine($"\t\tGot reference to block blob containers for file: {coursesFileName}");

                var coursesBlob = containerExporter.GetBlockBlobReference(coursesFileName);

                logFile.AppendLine(
                    $"\t\tAttempting to upload file {coursesFileName} to blob container {containerNameExporter}");

                await coursesBlob.UploadTextAsync(courses);

                log.LogInformation($"uploaded {coursesFileName}");

                logFile.AppendLine($"\t\tUploaded file {coursesFileName} to blob container {containerNameExporter}");

                return coursesFileName;
            }
            else
            {
                logFile.AppendLine($"\t\tHas no courses' data for: {mpItem}");
                return null;
            }
        }

    }


}
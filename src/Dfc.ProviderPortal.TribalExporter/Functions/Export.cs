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

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class Export
    {
        [FunctionName(nameof(Export))]
        public static async Task Run(
            [TimerTrigger("00 22 * * *")]TimerInfo myTimer,
            ILogger log,
            [Inject] IConfiguration configuration,
            [Inject] IBlobStorageHelper blobStorageHelper,
            [Inject] IProviderService providerService,
            [Inject] ICourseService courseService,
            [Inject] IVenueService venueService)
        {
            //TODO: add more logging after you get this working ...
            var logFile = new StringBuilder();
            logFile.AppendLine($"Starting {nameof(Export)} at {DateTime.Now}");

            var fileNames = new List<string>();
            var last24HoursAgo = DateTime.Today.AddDays(-1);
            var providersFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Providers_{DateTime.Today.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

            logFile.AppendLine($"24 Hours ago: {last24HoursAgo}");
            logFile.AppendLine($"Provider filename: {providersFileName}");

            var containerNameExporter = configuration["ContainerNameExporter"];
            var containerNameProviderFiles = configuration["ContainerNameProviderFiles"];

            logFile.AppendLine($"Attempting to get reference to bolb containers: {containerNameExporter}, {containerNameProviderFiles}");

            var containerExporter = blobStorageHelper.GetBlobContainer(containerNameExporter);
            var containerProviderFiles = blobStorageHelper.GetBlobContainer(containerNameProviderFiles);

            logFile.AppendLine($"Got references to bolb containers: {containerNameExporter}, {containerNameProviderFiles}");

            try
            {
                var migrationProviderCsv = configuration["MigrationProviderCsv"];

                logFile.AppendLine($"Attempting to get content from Migration Provider CSV file: {migrationProviderCsv}");

                var migrationProvidersCsvContent = await blobStorageHelper.ReadFileAsync(containerProviderFiles, migrationProviderCsv);

                logFile.AppendLine($"Got content from Migration Provider CSV file: {migrationProviderCsv} [content length: {migrationProvidersCsvContent.Length}]");

                logFile.AppendLine($"Attempting to deserialise content into a IEnumerable<{nameof(MiragtionProviderItem)}>");

                var mpItems = MigrationProviderItemHelper.GetMiragtionProviderItems(migrationProvidersCsvContent);

                logFile.AppendLine($"Start of deserialised content into: {mpItems.Count()} items in IEnumerable<{nameof(MiragtionProviderItem)}>");

                logFile.Append(string.Join("," + Environment.NewLine, mpItems));

                logFile.AppendLine($"{Environment.NewLine}End of deserialised content");

                var ukprns = mpItems.AsUkprns();

                if (ukprns != null && ukprns.Any())
                {
                    logFile.AppendLine($"Attempting to get providers' data for UKPRNS: {string.Join(",", ukprns)}");

                    var providers = await providerService.GetAllAsJsonAsync(ukprns);

                    logFile.AppendLine($"Got all the providers' data.");

                    logFile.AppendLine($"Attempting to get reference to block bolb containers for file: {providersFileName}");

                    var providersBlob = containerExporter.GetBlockBlobReference(providersFileName);

                    logFile.AppendLine($"Got reference to block bolb containers for file: {providersFileName}");

                    logFile.AppendLine($"Attempting to upload file {providersFileName} to blob container {containerNameExporter}");

                    await providersBlob.UploadTextAsync(providers);

                    logFile.AppendLine($"Uploaded file {providersFileName} to blob container {containerNameExporter}");

                    fileNames.Add(providersFileName);
                }

                foreach (var mpItem in mpItems)
                {
                    logFile.AppendLine($"Attempting to get conditional data for: {mpItem}");

                    var hasTodaysDate = mpItem.DateMigrated.Date == DateTime.Today;
                    var dateMigratedIsInThePast = mpItem.DateMigrated.Date < DateTime.Today;

                    var hasCreatedCourses = await courseService.HasCoursesBeenCreatedSinceAsync(mpItem.Ukprn, last24HoursAgo);
                    var hasCreatedCourseRuns = await courseService.HasCourseRunsBeenCreatedSinceAsync(mpItem.Ukprn, last24HoursAgo);

                    var hasUpdatedCourses = await courseService.HasCoursesBeenUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);
                    var hasUpdatedCourseRuns = await courseService.HasCourseRunsBeenUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);

                    var hasDeletedCourses = await courseService.HasCoursesBeenDeletedSinceAsync(mpItem.Ukprn, last24HoursAgo);
                    var hasDeletedCourseRuns = await courseService.HasCourseRunsBeenDeletedSinceAsync(mpItem.Ukprn, last24HoursAgo);

                    var hasUpdatedVenues = await venueService.HasBeenAnUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);

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

                    if (hasTodaysDate 
                        || (dateMigratedIsInThePast 
                            && (hasCreatedCourses || hasCreatedCourseRuns || hasUpdatedCourses || hasUpdatedCourseRuns || hasDeletedCourses || hasDeletedCourseRuns ||
                                hasUpdatedVenues)))
                    {

                        logFile.AppendLine($"Conditional logic for {mpItem} is True.");

                        logFile.AppendLine($"\tAttempting to get courses' data for: {mpItem}");

                        var courses = await courseService.GetAllLiveCoursesAsJsonForUkprnAsync(mpItem.Ukprn);

                        logFile.AppendLine($"\tGot courses' data for: {mpItem}");

                        if (courses != "[]")
                        {
                            logFile.AppendLine($"\t\tHas courses' data for: {mpItem}");

                            var coursesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Courses_for_Providers_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                            logFile.AppendLine($"\t\tGot reference to block bolb containers for file: {coursesFileName}");

                            var coursesBlob = containerExporter.GetBlockBlobReference(coursesFileName);

                            logFile.AppendLine($"\t\tGot reference to block bolb containers for file: {coursesFileName}");

                            logFile.AppendLine($"\t\tAttempting to upload file {coursesFileName} to blob container {containerNameExporter}");

                            await coursesBlob.UploadTextAsync(courses);

                            logFile.AppendLine($"\t\tUploaded file {coursesFileName} to blob container {containerNameExporter}");

                            fileNames.Add(coursesFileName);
                        }
                        else
                        {
                            logFile.AppendLine($"\t\tHas no courses' data for: {mpItem}");
                        }

                        logFile.AppendLine($"\tAttempting to get venues' data for: {mpItem}");

                        var venues = await venueService.GetAllVenuesAsJsonForUkprnAsync(mpItem.Ukprn);

                        logFile.AppendLine($"\tGot venues' data for: {mpItem}");

                        if (venues != "[]")
                        {
                            logFile.AppendLine($"\t\tHas venues' data for: {mpItem}");

                            var venuesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Venues_for_Providers_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                            logFile.AppendLine($"\t\tGot reference to block bolb containers for file: {venuesFileName}");

                            var venuesBlob = containerExporter.GetBlockBlobReference(venuesFileName);

                            logFile.AppendLine($"\t\tGot reference to block bolb containers for file: {venuesFileName}");

                            logFile.AppendLine($"\t\tAttempting to upload file {venuesFileName} to blob container {containerNameExporter}");

                            await venuesBlob.UploadTextAsync(venues);

                            logFile.AppendLine($"\t\tUploaded file {venuesFileName} to blob container {containerNameExporter}");

                            fileNames.Add(venuesFileName);
                        }
                        else
                        {
                            logFile.AppendLine($"\t\tHas no venues' data for: {mpItem}");
                        }
                    }
                    else
                    {
                        logFile.AppendLine($"Conditional logic for {mpItem} is False.");
                    }
                }

                var fileNamesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\FileNames.json";
                var fileNamesBlob = containerExporter.GetBlockBlobReference(fileNamesFileName);
                await fileNamesBlob.UploadTextAsync(JsonConvert.SerializeObject(fileNames, Formatting.Indented));
            }
            catch (Exception e)
            {
                logFile.AppendLine(e.Message);
                logFile.AppendLine(e.ToString());
            }
            finally
            {
                logFile.AppendLine($"Ending {nameof(Export)} at {DateTime.Now}");
                var logFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Log_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.txt";
                var logFileNameBolb = containerExporter.GetBlockBlobReference(logFileName);
                await logFileNameBolb.UploadTextAsync(logFile.ToString());
            }
        }
    }
}
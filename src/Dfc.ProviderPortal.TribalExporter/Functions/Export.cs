using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            [Inject] IMigrationProviderFileHelper migrationProviderFileHelper,
            [Inject] IProviderService providerService,
            [Inject] ICourseService courseService,
            [Inject] IVenueService venueService)
        {
            log.LogInformation($"Timer trigger function {nameof(Export)} started executing at: {DateTime.Now}");

            //TODO: add more logging after you get this working ...

            var fileNames = new List<string>();
            var last24HoursAgo = DateTime.Today.AddDays(-1);
            var providersFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Providers_{DateTime.Today.ToString("yyyy-MM-ddTHH-mm-ss")}.json";
            var container = blobStorageHelper.GetBlobContainer(configuration["ContainerName"]);
            var mpItems = await migrationProviderFileHelper.GetItemsAsync(container, configuration["MigrationProviderCsv"]);
            var ukprns = mpItems.AsUkprns();

            if (ukprns != null && ukprns.Any())
            {
                var providers = await providerService.GetAllAsJsonAsync(ukprns);
                var providersBlob = container.GetBlockBlobReference(providersFileName);
                await providersBlob.UploadTextAsync(providers);
                fileNames.Add(providersFileName);
            }

            foreach (var mpItem in mpItems)
            {
                var hasTodaysDate = mpItem.DateMigrated.Date == DateTime.Today;
                var hasUpdatedVenues = await venueService.HasBeenAnUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);
                var hasUpdatedCourses = await courseService.HasCoursesBeenUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);
                var hasUpdatedCourseRuns = await courseService.HasCourseRunsBeenUpdatedSinceAsync(mpItem.Ukprn, last24HoursAgo);

                if (hasTodaysDate || hasUpdatedVenues || hasUpdatedCourses || hasUpdatedCourseRuns)
                {
                    var courses = await courseService.GetAllLiveCoursesAsJsonForUkprnAsync(mpItem.Ukprn);
                    if (courses != "[]")
                    {
                        var coursesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Courses_for_Providers_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";
                        var coursesBlob = container.GetBlockBlobReference(coursesFileName);
                        await coursesBlob.UploadTextAsync(courses);
                        fileNames.Add(coursesFileName);
                    }

                    var venues = await venueService.GetAllVenuesAsJsonForUkprnAsync(mpItem.Ukprn);
                    if (venues != "[]")
                    {
                        var venuesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\Venues_for_Providers_{mpItem.Ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";
                        var venuesBlob = container.GetBlockBlobReference(venuesFileName);
                        await venuesBlob.UploadTextAsync(venues);
                        fileNames.Add(venuesFileName);
                    }
                }
            }

            var fileNamesFileName = $"TEST_IGNORE_{DateTime.Today.ToString("yyyyMMdd")}\\Generated\\FileNames.json";
            var fileNamesBlob = container.GetBlockBlobReference(fileNamesFileName);
            await fileNamesBlob.UploadTextAsync(JsonConvert.SerializeObject(fileNames, Formatting.Indented));
        }
    }
}
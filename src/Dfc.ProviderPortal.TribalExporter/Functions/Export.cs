using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class Export
    {
        [FunctionName(nameof(Export))]
        public static async Task Run(
            [TimerTrigger("%ExportTimerTriggerSchedule%")]TimerInfo myTimer,
            ILogger log,
            [Inject] IConfiguration configuration,
            [Inject] IBlobStorageHelper blobStorageHelper,
            [Inject] IProviderService providerService,
            [Inject] ICourseService courseService,
            [Inject] IVenueService venueService)
        {
            log.LogInformation($"Timer trigger function {nameof(Export)} started executing at: {DateTime.Now}");

            var today = DateTime.Now;
            var fileNames = new List<string>();

            var providersFileName = $"{today.ToString("yyyyMMdd")}\\Generated\\Providers_{today.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

            log.LogInformation($"Timer trigger function {nameof(Export)} starting to get data to create {providersFileName} at {DateTime.Now}");

            var container = blobStorageHelper.GetBlobContainer(configuration["ContainerName"]);
            var providers = await providerService.GetAllAsJsonAsync();
            var providersBlob = container.GetBlockBlobReference(providersFileName);

            log.LogInformation($"Timer trigger function {nameof(Export)} got all data to create {providersFileName} at {DateTime.Now}");

            await providersBlob.UploadTextAsync(providers);
            fileNames.Add(providersFileName);

            log.LogInformation($"Timer trigger function {nameof(Export)} created and uploaded {providersFileName} at {DateTime.Now}");

            log.LogInformation($"Timer trigger function {nameof(Export)} starting to get all UKPRNs at {DateTime.Now}");

            var ukprns = await providerService.GetAllUkprnsAsync();

            log.LogInformation($"Timer trigger function {nameof(Export)} got all UKPRNs at {DateTime.Now}");

            foreach (var ukprn in ukprns)
            {
                log.LogInformation($"Timer trigger function {nameof(Export)} starting to get course data for {ukprn} at {DateTime.Now}");

                var courses = await courseService.GetAllLiveCoursesAsJsonForUkprnAsync(ukprn);

                log.LogInformation($"Timer trigger function {nameof(Export)} got all course data for {ukprn} at {DateTime.Now}");

                if (courses != "[]")
                {
                    var coursesFileName = $"{today.ToString("yyyyMMdd")}\\Generated\\Courses_for_Providers_{ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                    log.LogInformation($"Timer trigger function {nameof(Export)} starting to create {coursesFileName} at {DateTime.Now}");

                    var coursesBlob = container.GetBlockBlobReference(coursesFileName);
                    await coursesBlob.UploadTextAsync(courses);
                    fileNames.Add(coursesFileName);

                    log.LogInformation($"Timer trigger function {nameof(Export)} created {coursesFileName} at {DateTime.Now}");
                }
                else
                {
                    log.LogInformation($"Timer trigger function {nameof(Export)} no course data for {ukprn} at {DateTime.Now}");
                }
            }

            foreach (var ukprn in ukprns)
            {
                log.LogInformation($"Timer trigger function {nameof(Export)} starting to get venues data for {ukprn} at {DateTime.Now}");

                var venues = await venueService.GetAllVenuesAsJsonForUkprnAndDateAsync(ukprn, DateTime.Now);

                log.LogInformation($"Timer trigger function {nameof(Export)} got all venue data for {ukprn} at {DateTime.Now}");

                if (venues != "[]")
                {
                    var venuesFileName = $"{today.ToString("yyyyMMdd")}\\Generated\\Venues_for_Providers_{ukprn}_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

                    log.LogInformation($"Timer trigger function {nameof(Export)} starting to create {venuesFileName} at {DateTime.Now}");

                    var venuesBlob = container.GetBlockBlobReference(venuesFileName);
                    await venuesBlob.UploadTextAsync(venues);
                    fileNames.Add(venuesFileName);

                    log.LogInformation($"Timer trigger function {nameof(Export)} created {venuesFileName} at {DateTime.Now}");
                }
                else
                {
                    log.LogInformation($"Timer trigger function {nameof(Export)} no venue data for {ukprn} at {DateTime.Now}");
                }
            }

            var fileNamesFileName = $"{today.ToString("yyyyMMdd")}\\Generated\\FileNames.json";

            log.LogInformation($"Timer trigger function {nameof(Export)} starting to create {fileNamesFileName} at {DateTime.Now}");

            var fileNamesBlob = container.GetBlockBlobReference(fileNamesFileName);
            await fileNamesBlob.UploadTextAsync(JsonConvert.SerializeObject(fileNames, Formatting.Indented));

            log.LogInformation($"Timer trigger function {nameof(Export)} created {fileNamesFileName} at {DateTime.Now}");

            var emptyFileName = $"{today.ToString("yyyyMMdd")}\\Error\\empty.json";

            log.LogInformation($"Timer trigger function {nameof(Export)} starting to create {emptyFileName} at {DateTime.Now}");

            var emptyBlob = container.GetBlockBlobReference(emptyFileName);
            await emptyBlob.UploadTextAsync(string.Empty);

            log.LogInformation($"Timer trigger function {nameof(Export)} created {emptyFileName} at {DateTime.Now}");

            log.LogInformation($"Timer trigger function {nameof(Export)} ended executing at: {DateTime.Now}");
        }
    }
}
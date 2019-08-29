using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Services.Interfaces;
using Dfc.CourseDirectory.Services.Interfaces.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces.CourseService;
using Dfc.CourseDirectory.Services.Interfaces.CourseTextService;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    class ApprenticeshipMigrationFunction
    {
        //[FunctionName(nameof(ApprenticeshipMigrationFunction))]
        //public static async Task Run(
        //    [TimerTrigger("%ApprenticeshipMigrationSchedule%")]
        //    TimerInfo myTimer,
        //    ILogger logger,
        //    [Inject] IConfigurationRoot configuration,
        //    [Inject] IVenueService venueService,
        //    [Inject] ILarsSearchService larsSearchService,
        //    [Inject] ICourseService courseService,
        //    [Inject] ICourseTextService courseTextService,
        //    [Inject] IProviderService providerService,
        //    [Inject] IBlobStorageService blobService)
        //{
        //}
    }
}

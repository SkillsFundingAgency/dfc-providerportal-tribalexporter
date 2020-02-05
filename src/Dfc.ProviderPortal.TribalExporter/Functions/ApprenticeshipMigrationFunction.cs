using Dfc.CourseDirectory.Services.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces;
using Dfc.CourseDirectory.Services.Interfaces.CourseService;
using Dfc.CourseDirectory.Services.Interfaces.CourseTextService;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public class ApprenticeshipMigrationFunction
    {
        [Disable]
        [FunctionName(nameof(ApprenticeshipMigrationFunction))]
        public static async Task Run(
            [TimerTrigger("%ApprenticeshipMigrationSchedule%")]
            TimerInfo myTimer,
            ILogger logger,
            [Inject] IApprenticeshipMigration apprenticeshipMigration)
        {
            await apprenticeshipMigration.RunApprenticeShipMigration(logger);
        }
    }
}

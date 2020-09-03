using System.Threading.Tasks;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class GenerateMigrationReport
    {
        [FunctionName(nameof(GenerateMigrationReport))]
        public static async Task Run(
                    [TimerTrigger("%MigrationReportSchedule%")]TimerInfo myTimer,
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobHelper,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICourseCollectionService courseCollectionService,
                    [Inject] IApprenticeshipCollectionService apprenticeshipCollectionService,
                    [Inject] IMigrationReportCollectionService migrationReportCollectionService)
        {
            await new MigrationReportGeneratorService().Run(
                log, configuration, cosmosDbHelper, blobHelper, providerCollectionService, courseCollectionService,
                apprenticeshipCollectionService, migrationReportCollectionService
            );
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ApprenticeshipDeltaExport
    {
        [Disable]
        [FunctionName(nameof(ApprenticeshipDeltaExport))]
        public static async Task Run(
                    [TimerTrigger("%ApprenticeshipMigrationSchedule%")]TimerInfo myTimer,
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IBlobStorageHelper blobStorageHelper,
                    [Inject] IApprenticeshipServiceWrapper apprenticeshipServiceWrapper)
        {
            var logFile = new StringBuilder();
            logFile.AppendLine($"Starting {nameof(ApprenticeshipDeltaExport)} at {DateTime.Now}");

            var fileNames = new List<string>();
            var last24HoursAgo = DateTime.Today.AddDays(-1);
            var providersFileName = $"{DateTime.Today.ToString("yyyyMMdd")}\\Apprenticeships\\Apprenticeships_for_Providers_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.json";

            logFile.AppendLine($"24 Hours ago: {last24HoursAgo}");
            logFile.AppendLine($"Apprenticeship Delta filename: {providersFileName}");

            var containerNameExporter = configuration["ContainerNameExporter"];
            var containerExporter = blobStorageHelper.GetBlobContainer(containerNameExporter);

            try
            {
                logFile.AppendLine($"Attempting to call Apprenticeship API to gather apprenticeship delta updates");
                var apprenticeshipDelta = apprenticeshipServiceWrapper.GetApprenticeshipDeltaUpdatesAsJson();
                logFile.AppendLine($"Successful call to apprenticeship API");
                if (apprenticeshipDelta != null)
                {
                    logFile.AppendLine($"Apprenticeship Delta JSON returned");
                    var providersBlob = containerExporter.GetBlockBlobReference(providersFileName);
                    logFile.AppendLine($"Attempting to upload apprenticeship delta json");
                    await providersBlob.UploadTextAsync(apprenticeshipDelta);
                    logFile.AppendLine($"Upload successful");
                }
                else
                {
                    logFile.AppendLine($"No updated apprenticeships between {last24HoursAgo} and {DateTime.Now} ");
                }

            }
            catch (Exception e)
            {
                logFile.AppendLine(e.Message);
                logFile.AppendLine(e.ToString());
            }
            finally
            {
                logFile.AppendLine($"Ending {nameof(ApprenticeshipDeltaExport)} at {DateTime.Now}");
                var logFileName = $"{DateTime.Today.ToString("yyyyMMdd")}\\Apprenticeships\\Logs\\Log_{DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}.txt";
                var logFileNameBolb = containerExporter.GetBlockBlobReference(logFileName);
                await logFileNameBolb.UploadTextAsync(logFile.ToString());
            }
        }
    }
}

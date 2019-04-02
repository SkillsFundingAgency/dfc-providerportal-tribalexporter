using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class Providers
    {
        [FunctionName("Providers")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
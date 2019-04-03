using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class Export
    {
        [FunctionName("Export")]
        public static void Run(
            [TimerTrigger("0 */10 * * * *")]TimerInfo myTimer,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            // TODO: do more stuff here ...
        }
    }
}
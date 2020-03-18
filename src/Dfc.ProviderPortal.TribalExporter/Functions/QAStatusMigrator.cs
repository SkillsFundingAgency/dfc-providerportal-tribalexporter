using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data.SqlClient;
using System.Linq;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class QAStatusMigrator
    {
        /// <summary>
        /// This function is intended archive venues that are deemed duplicates, and update the the corressponding courses/apprenticeships to reference the
        /// current verion of the venue.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="configuration"></param>
        /// <param name="cosmosDbHelper"></param>
        /// <param name="blobHelper"></param>
        /// <param name="loggerFactory"></param>
        /// <returns></returns>
        [FunctionName(nameof(QAStatusMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory)
        {

            var tribalConnectionString = configuration.GetConnectionString("TribalRestore");
            var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
            var qaStatuses = new List<ProviderQAStatus>();

            using (var sqlConnection = new SqlConnection(tribalConnectionString))
            {
                var sql = @"
SELECT PassedOverallQAChecks,Ukprn
FROM [Provider]";
                qaStatuses = sqlConnection.Query<ProviderQAStatus>(sql).ToList() ;
            }

            //look up id in provider

            foreach(var s in qaStatuses)
            {
                Console.WriteLine($"{s.UKPRN} - {s.PassedOverallQAChecks}");
            }

        }
    }

    public class ProviderQAStatus
    {
        public int UKPRN { get; set; }
        public int PassedOverallQAChecks { get; set; }
    }
}

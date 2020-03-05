using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class DeleteInvalidVenues
    {
        /// <summary>
        /// This is a function to remove invalid venues from cosmos (venues that were previously migrated with no lat/long.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="configuration"></param>
        /// <param name="cosmosDbHelper"></param>
        /// <param name="loggerFactory"></param>
        /// <returns></returns>
        [FunctionName(nameof(DeleteInvalidVenues))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    [Inject] IConfigurationRoot configuration,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] ILoggerFactory loggerFactory
                    )
        {
            var logger = loggerFactory.CreateLogger(typeof(DeleteInvalidVenues));
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var venueCollectionId = "venues";
            var documentClient = cosmosDbHelper.GetClient();
            var venueCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venueCollectionId);
            var deleteCount = 0;
            string continuation = null;

            do
            {
                var feedOptions = new FeedOptions()
                {
                    RequestContinuation = continuation
                };

                var queryResponse = await documentClient.CreateDocumentQuery<Venue>(venueCollectionUri, feedOptions)
                                    .Where(p => p.Latitude == null || p.Longitude == null)
                                    .AsDocumentQuery()
                                    .ExecuteNextAsync<Venue>();

                foreach (var ven in queryResponse)
                {
                    var item = UriFactory.CreateDocumentUri(databaseId, venueCollectionId, ven.ID);
                    await documentClient.DeleteDocumentAsync(item);
                    deleteCount++;
                    logger.LogInformation($"Deleted venue: {ven.VenueID}");
                }
                continuation = queryResponse.ResponseContinuation;
            }
            while (continuation != null);

            Console.WriteLine($"Deleted {deleteCount} venues with missing lat/long.");
            logger.LogError($"Deleted {deleteCount} venues with missing lat/long.");

        }
    }
}

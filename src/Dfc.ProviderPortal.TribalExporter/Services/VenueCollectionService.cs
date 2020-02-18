using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class VenueCollectionService : IVenueCollectionService, IDisposable
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;
        private readonly DocumentClient _documentClient;

        public VenueCollectionService(
            ICosmosDbHelper cosmosDbHelper,
            IOptions<CosmosDbSettings> cosmosDbSettings,
            IOptions<CosmosDbCollectionSettings> cosmosDbCollectionSettings)
        {
            Throw.IfNull(cosmosDbHelper, nameof(cosmosDbHelper));
            Throw.IfNull(cosmosDbSettings, nameof(cosmosDbSettings));
            Throw.IfNull(cosmosDbCollectionSettings, nameof(cosmosDbCollectionSettings));

            _cosmosDbHelper = cosmosDbHelper;
            _cosmosDbSettings = cosmosDbSettings.Value;
            _cosmosDbCollectionSettings = cosmosDbCollectionSettings.Value;
            _documentClient = _cosmosDbHelper.GetClient();
        }

        public void Dispose() => _documentClient.Dispose();

        public async Task<string> GetAllVenuesAsJsonForUkprnAsync(int ukprn)
        {
            var documents = new List<Document>();

            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
                var sql = $"SELECT * FROM c WHERE c.UKPRN = {ukprn} AND c.Status = 1";
                var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

                using (var query = _documentClient.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        foreach (var document in await query.ExecuteNextAsync<Document>()) documents.Add(document);
                    }
                }
            }

            return JsonConvert.SerializeObject(documents, Formatting.Indented);
        }

        // NOTE: There is no Venue Status of "Updated" however this should cover all Created, Updated, Deleting usecases if the DateUpdated is changed in all usecases.
        public async Task<bool> HasBeenAnUpdatedSinceAsync(int ukprn, DateTime date)
        {
            var documents = new List<Document>();

            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
                var sql = $"SELECT * FROM c WHERE c.UKPRN = {ukprn} AND c.DateUpdated > '{date.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}'";
                var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

                using (var query = _documentClient.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        foreach (var document in await query.ExecuteNextAsync<Document>()) documents.Add(document);
                    }
                }
            }

            return documents.Count() > 0;
        }

        public async Task<Venue> GetDocumentByVenueId(int venueId)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
            var sql = $"SELECT* FROM c WHERE c.VENUE_ID = {venueId}";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
            var query = _documentClient.CreateDocumentQuery<Venue>(uri, sql, options).AsDocumentQuery();
            return (await query.ExecuteNextAsync()).OrderByDescending(x => x.DateCreated).FirstOrDefault();
        }
         
        public async Task<Venue> GetDocumentByLocationId(int locationId)
        {

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
            var sql = $"SELECT* FROM c WHERE c.LocationId = { locationId }";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
            var query = _documentClient.CreateDocumentQuery<Venue>(uri, sql, options).AsDocumentQuery();
            return (await query.ExecuteNextAsync()).OrderByDescending(x => x.DateCreated).FirstOrDefault();
        }
    }
}



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
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class VenueService : IVenueService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public VenueService(
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
        }

        public async Task<string> GetAllVenuesAsJsonForUkprnAndAfterDateAsync(int ukprn, DateTime afterDate)
        {
            var documents = new List<Document>();

            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
                var sql = $"SELECT * FROM c WHERE c.UKPRN = {ukprn} AND c.DateUpdated > '{afterDate.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}'";
                var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
                var client = _cosmosDbHelper.GetClient();

                using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                {
                    while (query.HasMoreResults)
                    {
                        foreach (var document in await query.ExecuteNextAsync<Document>()) documents.Add(document);
                    }
                }
            }

            return JsonConvert.SerializeObject(documents, Formatting.Indented);
        }
    }
}
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
    public class ProviderCollectionService : IProviderCollectionService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public ProviderCollectionService(
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

        public async Task<string> GetAllAsJsonAsync(IEnumerable<int> ukprns)
        {
            if (ukprns == null) throw new ArgumentException(nameof(ukprns));

            var documents = new List<Document>();
            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.ProvidersCollectionId);
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            using (var client = _cosmosDbHelper.GetClient())
            {
                if (ukprns.Any())
                {
                    var commaDelimList = $"'{string.Join("','", ukprns)}'";
                    var sql = $"SELECT * FROM c WHERE c.Status = 1 AND c.UnitedKingdomProviderReferenceNumber IN ({commaDelimList})";
                    using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                    {
                        while (query.HasMoreResults)
                        {
                            foreach (var document in await query.ExecuteNextAsync<Document>()) documents.Add(document);
                        }
                    }
                }
            }

            return JsonConvert.SerializeObject(documents, Formatting.Indented);
        }
    }
}
using Dfc.CourseDirectory.Models.Models.Providers;
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
    public class FEChoicesDataCollectionService : IFEChoicesDataCollectionService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;
        private readonly DocumentClient _client;

        public FEChoicesDataCollectionService(
            ICosmosDbHelper cosmosDbHelper,
            IOptions<CosmosDbSettings> cosmosDbSettings,
            IOptions<CosmosDbCollectionSettings> cosmosDbCollectionSettings)
        {
            Throw.IfNull(cosmosDbHelper, nameof(cosmosDbHelper));
            Throw.IfNull(cosmosDbSettings, nameof(cosmosDbSettings));
            Throw.IfNull(cosmosDbCollectionSettings, nameof(cosmosDbCollectionSettings));

            _cosmosDbHelper = cosmosDbHelper;
            _client = cosmosDbHelper.GetClient();
            _cosmosDbSettings = cosmosDbSettings.Value;
            _cosmosDbCollectionSettings = cosmosDbCollectionSettings.Value;
        }

        public async Task<List<FEChoicesData>> GetAllDocument()
        {
            var documents = new List<Document>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.FEChoicesDataCollectionId);
            var sql = $"SELECT * FROM p";

            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var providerList = new List<FEChoicesData>();
            var query = _client.CreateDocumentQuery<Document>(uri, sql, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                foreach (var document in await query.ExecuteNextAsync<FEChoicesData>()) providerList.Add(document);
            }

            return providerList;
        }

        public async Task<FEChoicesData> GetDocumentByUkprn(int ukprn)
        {
            var documents = new List<Document>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.FEChoicesDataCollectionId);
            var sql = $"SELECT * FROM p WHERE p.UKPRN = {ukprn}";

            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var providerList = new List<FEChoicesData>();
            var query = _client.CreateDocumentQuery<Document>(uri, sql, options).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                foreach (var document in await query.ExecuteNextAsync<FEChoicesData>()) providerList.Add(document);
            }

            return providerList.OrderByDescending(x => x.CreatedDateTimeUtc).FirstOrDefault();
        }
    }
}
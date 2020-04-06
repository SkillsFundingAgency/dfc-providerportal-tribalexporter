using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class ApprenticeshipCollectionService : IApprenticeshipCollectionService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public ApprenticeshipCollectionService(
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

        public async Task<List<Apprenticeship>> GetAllApprenticeshipsAsync()
        {
            var documents = new List<Apprenticeship>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.ApprenticeshipCollectionId);
            var sql = $"SELECT * FROM a";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var client = _cosmosDbHelper.GetClient();
            using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
            {
                while (query.HasMoreResults)
                {
                    foreach (Apprenticeship document in await query.ExecuteNextAsync<Apprenticeship>()) documents.Add(document);
                }
            }

            return documents;
        }

        public async Task<List<Apprenticeship>> GetArchivedApprenticeshipsAsync()
        {
            var documents = new List<Apprenticeship>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.ApprenticeshipCollectionId);
            var sql = $"SELECT * FROM a WHERE a.RecordStatus = {(int)CourseDirectory.Models.Enums.RecordStatus.MigrationPending}";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var client = _cosmosDbHelper.GetClient();
            using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
            {
                while (query.HasMoreResults)
                {
                    foreach (Apprenticeship document in await query.ExecuteNextAsync<Apprenticeship>()) documents.Add(document);
                }
            }

            return documents;
        }

        public async Task<List<Apprenticeship>> GetAllApprenticeshipsByUkprnAsync(string ukprn)
        {
            var documents = new List<Apprenticeship>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.ApprenticeshipCollectionId);
            var sql = $"SELECT * FROM a WHERE a.ProviderUKPRN = {ukprn} AND a.RecordStatus <> {(int)CourseDirectory.Models.Enums.RecordStatus.Archived}";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var client = _cosmosDbHelper.GetClient();
            using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
            {
                while (query.HasMoreResults)
                {
                    foreach (Apprenticeship document in await query.ExecuteNextAsync<Apprenticeship>()) documents.Add(document);
                }
            }

            return documents;
        }
    }
}

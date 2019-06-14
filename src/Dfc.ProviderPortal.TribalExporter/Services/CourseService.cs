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
    public class CourseService : ICourseService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public CourseService(
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

        public async Task<string> GetAllLiveCoursesAsJsonForUkprnAsync(int ukprn)
        {
            var documents = new List<Document>();

            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.CoursesCollectionId);
                var sql = $"SELECT * FROM c WHERE c.ProviderUKPRN = {ukprn} AND ARRAY_CONTAINS(c.CourseRuns, {{ RecordStatus: 1 }}, true)";
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

        public async Task<bool> HasCoursesBeenUpdatedSinceAsync(int ukprn, DateTime date)
        {
            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.CoursesCollectionId);
                var sql = $"SELECT * FROM c WHERE c.ProviderUKPRN = {ukprn} AND ARRAY_CONTAINS(c.CourseRuns, {{ RecordStatus: 1 }}, true) AND c.UpdatedDate > '{date.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}'";
                var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
                var client = _cosmosDbHelper.GetClient();

                using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                {
                    return query.HasMoreResults;
                }
            }

            return false;
        }

        public async Task<bool> HasCourseRunsBeenUpdatedSinceAsync(int ukprn, DateTime date)
        {
            if (ukprn > 0)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.CoursesCollectionId);
                var sql = $"SELECT c.id FROM c JOIN cr IN c.CourseRuns WHERE cr.RecordStatus = 1 AND  cr.UpdatedDate > '{date.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}' AND c.ProviderUKPRN = {ukprn}";
                var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
                var client = _cosmosDbHelper.GetClient();

                using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
                {
                    return query.HasMoreResults;
                }
            }

            return false;
        }
    }
}
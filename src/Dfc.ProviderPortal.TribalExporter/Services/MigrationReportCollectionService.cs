using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Reports;
using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class MigrationReportCollectionService : IMigrationReportCollectionService
    {
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public MigrationReportCollectionService(
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

        public async Task<MigrationReportEntry> GetReportForApprenticeshipByUkprn(int ukprn)
        {
            var documents = new List<MigrationReportEntry>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.MigrationReportApprenticeshipCollectionId);
            var sql = $"SELECT * FROM a WHERE a.id = \"{ukprn}\"";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var client = _cosmosDbHelper.GetClient();
            using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
            {
                while (query.HasMoreResults)
                {
                    foreach (MigrationReportEntry document in await query.ExecuteNextAsync<MigrationReportEntry>()) documents.Add(document);
                }
            }

            return documents.FirstOrDefault();
        }

        public async Task<MigrationReportEntry> GetReportForCoursesByUkprn(int ukprn)
        {
            var documents = new List<MigrationReportEntry>();

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.MigrationReportCoursesCollectionId);
            var sql = $"SELECT * FROM a WHERE a.id = \"{ukprn}\"";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };

            var client = _cosmosDbHelper.GetClient();
            using (var query = client.CreateDocumentQuery(uri, sql, options).AsDocumentQuery())
            {
                while (query.HasMoreResults)
                {
                    foreach (MigrationReportEntry document in await query.ExecuteNextAsync<MigrationReportEntry>()) documents.Add(document);
                }
            }

            return documents.FirstOrDefault();
        }


    }
}

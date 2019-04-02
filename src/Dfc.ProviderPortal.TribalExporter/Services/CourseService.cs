using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;

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

        public IEnumerable<ICourse> GetCourseForProvider(IProvider provider)
        {
            Throw.IfNull(provider, nameof(provider));

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.CoursesCollectionId);
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
            var client = _cosmosDbHelper.GetClient();
            var found = client.CreateDocumentQuery<Course>(uri, options).ToList(); // filter this more !!!

            return found ?? new List<Course>();
        }
    }
}
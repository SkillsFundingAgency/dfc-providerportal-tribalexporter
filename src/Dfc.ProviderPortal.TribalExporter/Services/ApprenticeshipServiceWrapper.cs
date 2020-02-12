using Dfc.CourseDirectory.Models.Interfaces.Apprenticeships;
using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Linq;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using System.Linq;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class ApprenticeshipServiceWrapper : IApprenticeshipServiceWrapper
    {
        private readonly IApprenticeshipServiceSettings _settings;
        private readonly ICosmosDbHelper _cosmosDbHelper;
        private readonly ICosmosDbSettings _cosmosDbSettings;
        private readonly ICosmosDbCollectionSettings _cosmosDbCollectionSettings;

        public ApprenticeshipServiceWrapper(IOptions<ApprenticeshipServiceSettings> settings, ICosmosDbHelper cosmosDbHelper,
            IOptions<CosmosDbSettings> cosmosDbSettings,
            IOptions<CosmosDbCollectionSettings> cosmosDbCollectionSettings)
        {
            Throw.IfNull(settings, nameof(settings));
            _settings = settings.Value;

            _cosmosDbHelper = cosmosDbHelper;
            _cosmosDbSettings = cosmosDbSettings.Value;
            _cosmosDbCollectionSettings = cosmosDbCollectionSettings.Value;
        }

        public async Task<Apprenticeship> GetApprenticeshipByApprenticeshipID(int apprenticeshipId)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.ApprenticeshipCollectionId);
            var sql = $"SELECT* FROM c WHERE c.ApprenticeshipID = { apprenticeshipId }";
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
            using (var client = _cosmosDbHelper.GetClient())
            {
                var query = client.CreateDocumentQuery<Apprenticeship>(uri, sql, options).AsDocumentQuery();
                return (await query.ExecuteNextAsync()).FirstOrDefault();
            };
        }

        public string GetApprenticeshipDeltaUpdatesAsJson()
        {
            // Call service to get data
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            Task<HttpResponseMessage> taskResponse = client.GetAsync($"{_settings.ApiUrl}/GetUpdatedApprenticeshipsAsProvider");
            taskResponse.Wait();
            Task<string> taskJSON = taskResponse.Result.Content.ReadAsStringAsync();
            taskJSON.Wait();
            string json = taskJSON.Result;

            // Return data as model objects
            if (!json.StartsWith("["))
                json = "[" + json + "]";
            client.Dispose();
            return json;
        }
    }
}

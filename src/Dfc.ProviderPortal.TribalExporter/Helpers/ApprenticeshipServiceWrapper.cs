using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class ApprenticeshipServiceWrapper : IApprenticeshipServiceWrapper
    {
        private readonly IApprenticeshipServiceSettings _settings;
        public ApprenticeshipServiceWrapper(ApprenticeshipServiceSettings settings)
        {
            Throw.IfNull(settings, nameof(settings));
            _settings = settings;
        }
        public IEnumerable<TribalProvider> GetApprenticeshipDeltaUpdates()
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
            return JsonConvert.DeserializeObject<IEnumerable<TribalProvider>>(json);
        }
    }
}

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
        }

        public IEnumerable<IVenue> GetVenuesByProvider(IProvider provider)
        {
            Throw.IfNull(provider, nameof(provider));

            var uri = UriFactory.CreateDocumentCollectionUri(_cosmosDbSettings.DatabaseId, _cosmosDbCollectionSettings.VenuesCollectionId);
            var options = new FeedOptions { EnableCrossPartitionQuery = true, MaxItemCount = -1 };
            var client = _cosmosDbHelper.GetClient();
            var found = client.CreateDocumentQuery<Venue>(uri, options).ToList(); // filter this more !!!

            return found ?? new List<Venue>();
        }
    }
}
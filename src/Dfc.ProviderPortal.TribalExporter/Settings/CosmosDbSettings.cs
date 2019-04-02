using Dfc.ProviderPortal.TribalExporter.Interfaces;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class CosmosDbSettings : ICosmosDbSettings
    {
        public string EndpointUri { get; set; }
        public string PrimaryKey { get; set; }
        public string DatabaseId { get; set; }
    }
}
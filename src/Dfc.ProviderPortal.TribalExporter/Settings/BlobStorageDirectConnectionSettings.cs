using Dfc.ProviderPortal.TribalExporter.Interfaces;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class BlobStorageDirectConnectionSettings : IBlobStorageDirectConnectionSettings
    {
        public string ConnectionString { get; set; }
    }
}
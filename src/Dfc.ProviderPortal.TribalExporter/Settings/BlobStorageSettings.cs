using Dfc.ProviderPortal.TribalExporter.Interfaces;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class BlobStorageSettings : IBlobStorageSettings
    {
        public string ConnectionString { get; set; }
    }
}
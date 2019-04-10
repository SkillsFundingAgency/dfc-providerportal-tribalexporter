using Microsoft.WindowsAzure.Storage.Blob;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IBlobStorageHelper
    {
        CloudBlobContainer GetBlobContainer(string containerName);
    }
}
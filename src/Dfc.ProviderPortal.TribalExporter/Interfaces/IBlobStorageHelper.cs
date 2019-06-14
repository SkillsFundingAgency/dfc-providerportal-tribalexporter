using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IBlobStorageHelper
    {
        CloudBlobContainer GetBlobContainer(string containerName);
        Task<string> ReadFileAsync(CloudBlobContainer container, string fileName);
    }
}
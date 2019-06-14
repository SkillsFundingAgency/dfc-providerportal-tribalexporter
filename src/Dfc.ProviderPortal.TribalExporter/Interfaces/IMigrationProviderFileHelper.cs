using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IMigrationProviderFileHelper
    {
        Task<IEnumerable<IMiragtionProviderItem>> GetItemsAsync(CloudBlobContainer container, string fileName);
    }
}

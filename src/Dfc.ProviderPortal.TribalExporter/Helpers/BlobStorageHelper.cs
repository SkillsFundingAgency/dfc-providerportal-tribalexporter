using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class BlobStorageHelper : IBlobStorageHelper
    {
        private readonly IBlobStorageSettings _settings;

        public BlobStorageHelper(IOptions<BlobStorageSettings> settings)
        {
            Throw.IfNull(settings, nameof(settings));

            _settings = settings.Value;
        }

        public CloudBlobContainer GetBlobContainer(string containerName)
        {
            if (CloudStorageAccount.TryParse(_settings.ConnectionString, out CloudStorageAccount storageAccount))
            {
                var client = storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference(containerName);

                return container;
            }
            else
            {
                throw new InvalidOperationException("Unable to access storage account.");
            }
        }
    }
}
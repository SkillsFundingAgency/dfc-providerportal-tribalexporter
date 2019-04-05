using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class BlobStorageHelper : IBlobStorageHelper
    {
        private readonly string _connectionString;

        public BlobStorageHelper(string connectionString)
        {
            Throw.IfNullOrWhiteSpace(connectionString, nameof(connectionString));

            _connectionString = connectionString;
        }

        public CloudBlobContainer GetBlobContainer(string containerName)
        {
            if (CloudStorageAccount.TryParse(_connectionString, out CloudStorageAccount storageAccount))
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

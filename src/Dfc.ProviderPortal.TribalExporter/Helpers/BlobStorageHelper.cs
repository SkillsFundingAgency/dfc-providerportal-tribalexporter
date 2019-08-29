﻿using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class BlobStorageHelper : IBlobStorageHelper
    {
        private readonly IBlobStorageDirectConnectionSettings _directConnectionSettings;

        public BlobStorageHelper(IOptions<BlobStorageDirectConnectionSettings> settings)
        {
            Throw.IfNull(settings, nameof(settings));

            _directConnectionSettings = settings.Value;
        }

        public CloudBlobContainer GetBlobContainer(string containerName)
        {
            if (CloudStorageAccount.TryParse(_directConnectionSettings.ConnectionString, out CloudStorageAccount storageAccount))
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

        public async Task<string> ReadFileAsync(CloudBlobContainer container, string fileName)
        {
            var text = string.Empty;
            var blob = container.GetBlockBlobReference(fileName);

            using (var memoryStream = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(memoryStream);
                text = Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            return text;
        }
    }
}
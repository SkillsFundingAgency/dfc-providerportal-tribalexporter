﻿
using Dfc.CourseDirectory.Common;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;


namespace Dfc.CourseDirectory.Services.BlobStorageService
{
    public class BlobFileInfo
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTimeOffset? DateUploaded { get; set; }
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly ILogger<BlobStorageService> _log;
        private readonly HttpClient _httpClient;

        private readonly string _accountName;

        private readonly string _accountKey;

        //private readonly string _containerName;
        //private readonly string _bulkUploadPathFormat;
        private readonly string _templatePath;
        private readonly string _providerListPath;

        private readonly CloudStorageAccount _account;

        //private readonly CloudBlobClient _blobClient;
        private readonly CloudBlobContainer _container;

        public BlobStorageService(
            ILogger<BlobStorageService> logger,
            HttpClient httpClient,
            BlobStorageSettings settings)
        {
            Throw.IfNull(logger, nameof(logger));
            Throw.IfNull(httpClient, nameof(httpClient));
            Throw.IfNull(settings, nameof(settings));

            _log = logger;
            _httpClient = httpClient;

            //_getSomethingByIdUri = settings.Value.ToGetSomethingByIdUri();
            _accountName = settings.AccountName;
            _accountKey = settings.AccountKey;
            //_containerName = settings.Value.Container;
            //_bulkUploadPathFormat = settings.Value.BulkUploadPathFormat;
            _templatePath = settings.TemplatePath;
            _providerListPath = settings.ProviderListPath;

            //Set up the client
            _account = new CloudStorageAccount(new StorageCredentials(_accountName, _accountKey), true);
            //_blobClient = _account.CreateCloudBlobClient();
            _container = _account.CreateCloudBlobClient()
                .GetContainerReference(settings.Container);
        }

        public async Task DownloadFile(string filePath, Stream stream)
        {
            try
            {
                _log.LogInformation($"File Path {filePath}");
                CloudBlockBlob blockBlob = _container.GetBlockBlobReference(filePath);

                if (await blockBlob.ExistsAsync())
                {
                    _log.LogInformation($"Downloading {filePath} from blob storage");
                    await blockBlob.DownloadToStreamAsync(stream);
                }
                else
                {
                    throw new Exception("Blockblob doesn't exist");
                }

            }
            catch (Exception ex)
            {
                _log.LogException($"Exception downloading {filePath}", ex);
            }
        }

        public Task UploadFileAsync(string filePath, Stream stream)
        {
            try
            {
                CloudBlockBlob blockBlob = _container.GetBlockBlobReference(filePath);
                ArchiveFiles(string.Join("/", filePath.Split('/').Reverse().Skip(1).Reverse()));

                stream.Position = 0;
                return blockBlob.UploadFromStreamAsync(stream);

                //} catch (StorageException stex) {
                //    _log.LogException($"Exception downloading {filePath}", stex);
                //    return null;

            }
            catch (Exception ex)
            {
                _log.LogException($"Exception uploading {filePath}", ex);
                return null;
            }
        }

        public IEnumerable<BlobFileInfo> GetFileList(string filePath)
        {
            try
            {
                return _container.GetDirectoryReference(filePath)
                    ?.ListBlobs()
                    ?.OfType<CloudBlockBlob>()
                    ?.Select(b => new BlobFileInfo()
                    { Name = b.Name, Size = b.Properties.Length, DateUploaded = b.Properties.Created });

                //} catch (StorageException stex) {
                //    _log.LogException($"Exception listing files at {filePath}", stex);
                //    return null;

            }
            catch (Exception ex)
            {
                _log.LogException($"Exception listing files at {filePath}", ex);
                return null;
            }
        }

        public IEnumerable<CloudBlockBlob> ArchiveFiles(string filePath)
        {
            try
            {
                IEnumerable<CloudBlockBlob> blobs = _container?.GetDirectoryReference(filePath)
                                                        ?.ListBlobs()
                                                        ?.OfType<CloudBlockBlob>()
                                                    ?? new CloudBlockBlob[] { };
                foreach (CloudBlockBlob b in blobs)
                {
                    _container.GetBlobReference($"Archive/{filePath.Substring(0, 8)}_{b.Uri.Segments.Last()}")
                        .StartCopy(b.Uri);
                    b.DeleteIfExists();
                }

                return blobs;

                //} catch (StorageException stex) {
                //    _log.LogException($"Exception archiving files at {filePath}", stex);
                //    return null;

            }
            catch (Exception ex)
            {
                _log.LogException($"Exception archiving files at {filePath}", ex);
                return null;
            }
        }

        public async Task GetBulkUploadTemplateFileAsync(Stream stream)
        {
           await DownloadFile(_templatePath, stream);
        }

        public async Task<List<int>> GetBulkUploadProviderListFile(int migrationHours)
        {
            var providerUKPRNList = new List<int>();
            var count = 1;
            string errors = string.Empty;

            try
            {
                _log.LogInformation("Getting Providers from Blob");

                MemoryStream ms = new MemoryStream();
                await DownloadFile(_providerListPath, ms);
                ms.Position = 0;

                using (StreamReader reader = new StreamReader(ms))
                {
                    string line = null;
                    while (null != (line = reader.ReadLine()))
                    {

                        string[] linedate = line.Split(',');

                        var provider = linedate[0];
                        var migrationdate = linedate[1];
                        var time = string.IsNullOrEmpty(linedate[2]) ? DateTime.Now.ToShortTimeString() : linedate[2];
                        DateTime migDate = DateTime.MinValue;
                        DateTime runTime = DateTime.MinValue;
                        int provID = 0;
                        DateTime.TryParseExact(migrationdate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out migDate);
                        DateTime.TryParse(time, out runTime);
                        migDate = migDate.Add(runTime.TimeOfDay);
                        int.TryParse(provider, out provID);
                        if (migDate > DateTime.MinValue && DateTimeWithinSpecifiedTime(migDate, migrationHours) && provID > 0)
                            providerUKPRNList.Add(provID);
                    }
                }
            }
            catch (Exception ex)
            { 
                _log.LogError("Failed textract line: " + count.ToString() + "Ex: " + ex.Message);
                throw;
            }

            return providerUKPRNList;

        }

        private bool DateTimeWithinSpecifiedTime(DateTime value, int hours)
        {
            return value <= DateTime.Now && value >= DateTime.Now.AddHours(-hours);
        }
    }
}

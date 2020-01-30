using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Azure.Documents.Client;
using Dfc.CourseDirectory.Models.Enums;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class VenueMigrator
    {
        /// <summary>
        /// This function app migrates venues from a sql db to cosmos outputing the results to 
        /// blob storage with a file name of VenueExport-{date}. This uses a provider whitelist (ProviderWhiteList.txt) file
        /// in the root of a blob storage container, which has a line for every whitelisted UKPRN.
        /// 
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <param name="configuration"></param>
        /// <param name="venueCollectionService"></param>
        /// <param name="providerCollectionService"></param>
        /// <param name="cosmosDbHelper"></param>
        /// <param name="blobhelper"></param>
        /// <returns></returns>
        [FunctionName(nameof(VenueMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IVenueCollectionService venueCollectionService,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper
                    )
        {
            var venuesCollectionId = configuration["CosmosDbCollectionSettings:VenuesCollectionId"];
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var whiteListProviders = await GetProviderWhiteList();
            var result = new List<ResultMessage>();
            var venueExportFileName = $"VenueExport-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";
            var ukprnCache = new List<int>();
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                                            SELECT  Ven.[VenueId],
                                                    Ven.[ProviderId],
                                                    Ven.[ProviderOwnVenueRef],
                                                    Ven.[VenueName],
                                                    Ven.[Email],
                                                    Ven.[Website],
                                                    Ven.[Fax],
                                                    Ven.[Facilities],
                                                    Ven.[RecordStatusId],
                                                    Ven.[CreatedByUserId],
                                                    Ven.[CreatedDateTimeUtc],
                                                    Ven.[ModifiedByUserId],
                                                    Ven.[ModifiedDateTimeUtc],
                                                    Ven.[AddressId],
                                                    Ven.[Telephone],
                                                    Ven.[BulkUploadVenueId],
                                                    Ad.AddressLine1,
                                                    ad.AddressLine2,
                                                    ad.Town,
                                                    ad.County,
                                                    ad.Postcode,
                                                    ad.[Latitude],
                                                    ad.[Longitude],
                                                    pr.ModifiedDateTimeUtc,
                                                    pr.ModifiedDateTimeUtc,
                                                    pr.Ukprn
                                              FROM Tribal.Venue Ven
                                              INNER JOIN Tribal.[Address] Ad on Ad.AddressId = Ven.AddressId
                                              INNER JOIN tribal.[Provider] pr on pr.ProviderId = ven.ProviderId
                                              WHERE Ven.RecordStatusID = 2";

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                //Read venue
                                var item = Venue.FromDataReader(dataReader);

                                if (await Validate(item))
                                {
                                    var cosmosVenue = await venueCollectionService.GetDocumentID(item.VenueId);
                                    if (cosmosVenue != null)
                                    {
                                        //Actual Update has been commented out until further notice, however we still want to write to the log
                                        //file that we would potentially update this venue record.
                                        //
                                        //var s = UriFactory.CreateDocumentUri(databaseId, venuesCollectionId, cosmosVenue.ID.ToString());
                                        //Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);
                                        //var editedVenue = new Dfc.CourseDirectory.Models.Models.Venues.Venue()
                                        //{
                                        //    ID = cosmosVenue.ID,
                                        //    UKPRN = item.UKPRN,
                                        //    VenueName = item.VenueName,
                                        //    Address1 = item.Address.Address1,
                                        //    Address2 = item.Address.Address2,
                                        //    Town = item.Address.Town,
                                        //    PostCode = item.Address.Postcode,
                                        //    Latitude = item.Address.Latitude,
                                        //    Longitude = item.Address.Longitude,
                                        //    Status = MapVenueStatus(item.RecordStatusId),
                                        //    UpdatedBy = "VenueMigrator",
                                        //    DateUpdated = DateTime.Now,
                                        //    VenueID = item.VenueId,
                                        //    ProviderID = item.ProviderId,
                                        //    ProvVenueID = item.ProviderOwnVenueRef,
                                        //    Email = item.Email,
                                        //    Website = item.Website,
                                        //    Telephone = item.Telephone,
                                        //    CreatedBy = item.CreatedByUserId,
                                        //    CreatedDate = item.CreatedDateTimeUtc

                                        //};
                                        //await cosmosDbHelper.GetClient().UpsertDocumentAsync(collectionUri, editedVenue);

                                        AddResultMessage(item.VenueId, "Venue Exists - Record not updated");
                                    }
                                    else
                                    {
                                        var newVenue = new Dfc.CourseDirectory.Models.Models.Venues.Venue()
                                        {
                                            UKPRN = item.UKPRN,
                                            VenueName = item.VenueName,
                                            Address1 = item.Address.Address1,
                                            Address2 = item.Address.Address2,
                                            Town = item.Address.Town,
                                            PostCode = item.Address.Postcode,
                                            Latitude = item.Address.Latitude,
                                            Longitude = item.Address.Longitude,
                                            Status = MapVenueStatus(item.RecordStatusId),
                                            UpdatedBy = item.CreatedByUserId,
                                            DateUpdated = item.CreatedDateTimeUtc,
                                            VenueID = item.VenueId,
                                            ProviderID = item.ProviderId,
                                            ProvVenueID = item.ProviderOwnVenueRef,
                                            Email = item.Email,
                                            Website = item.Website,
                                            Telephone = item.Telephone,
                                            CreatedDate = DateTime.Now,
                                            CreatedBy = "VenueMigrator"
                                        };
                                        await cosmosDbHelper.CreateDocumentAsync(cosmosDbHelper.GetClient(), venuesCollectionId, newVenue);

                                        //Log that successfully inserted venue
                                        AddResultMessage(item.VenueId, "Inserted Venue");
                                    }
                                }

                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                    }
                }
                var resultsObjBytes = GetResultAsByteArray(result);
                await WriteResultsToBlobStorage(resultsObjBytes);
            }

            CourseDirectory.Models.Models.Venues.VenueStatus MapVenueStatus(TribalRecordStatus recordStatus)
            {
                switch (recordStatus)
                {
                    case TribalRecordStatus.Pending: return CourseDirectory.Models.Models.Venues.VenueStatus.Pending;
                    case TribalRecordStatus.Live: return CourseDirectory.Models.Models.Venues.VenueStatus.Live;
                    case TribalRecordStatus.Archived: return CourseDirectory.Models.Models.Venues.VenueStatus.Archived;
                    case TribalRecordStatus.Deleted: return CourseDirectory.Models.Models.Venues.VenueStatus.Deleted;
                    default: throw new Exception("$Unable to map recordStatus to VenueStatus");
                }
            }

            byte[] GetResultAsByteArray(IList<ResultMessage> ob)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<ResultMessage>(ob);
                    }
                    return memoryStream.ToArray();
                }
            }

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, venueExportFileName, data);
            }

            async Task<IList<int>> GetProviderWhiteList()
            {
                var list = new List<int>();
                var whiteList = await blobhelper.ReadFileAsync(blobContainer, WHITE_LIST_FILE);
                if (!string.IsNullOrEmpty(whiteList))
                {
                    var lines = whiteList.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (string line in lines)
                    {
                        if (int.TryParse(line, out int id))
                        {
                            list.Add(id);
                        }
                    }
                }
                return list;
            }

            void AddResultMessage(int venueId, string status, string message = "")
            {
                var validateResult = new ResultMessage() { VenueId = venueId, Status = status, Message = message };
                result.Add(validateResult);
            }

            async Task<bool> Validate(Venue item)
            {
                //are providers on list of whitelisted providers file
                if (!whiteListProviders.Any(x => x == item.UKPRN))
                {
                    AddResultMessage(item.VenueId, "Failed", $"Provider {item.ProviderId} not on whitelist, ukprn {item.UKPRN}");
                    return false;
                }

                //check to see if a record is already held for ukprn
                if (!ukprnCache.Contains(item.UKPRN))
                {
                    var cosmosProvider = await providerCollectionService.ProviderExists(item.UKPRN);
                    if (!cosmosProvider)
                    {
                        AddResultMessage(item.VenueId, "Failed", "Unknown UKPRN");
                        return false;
                    }
                    else
                    {
                        //provider exists - add to cache
                        ukprnCache.Add(item.UKPRN);
                    }
                }
                return true;
            }
        }
    }
}

[Serializable()]
public class ResultMessage
{
    public int VenueId { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}
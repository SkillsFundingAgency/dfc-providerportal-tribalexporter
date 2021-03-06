﻿using System;
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
            var connectionString = configuration.GetConnectionString("TribalRestore");
            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var whiteListProviders = await GetProviderWhiteList();
            var result = new List<ResultMessage>();
            var venueList = new List<Venue>();
            var venueExportFileName = $"VenueExport-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";
            var ukprnCache = new List<int>();
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            //update or insert records
            var _cosmosClient = cosmosDbHelper.GetClient();

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                                            DECLARE @Venues TABLE
                                            (
		                                            VenueId INT NOT NULL,
		                                            ProviderId INT NOT NULL,
		                                            ProviderOwnVenueRef NVARCHAR(255)  NULL,
		                                            VenueName NVARCHAR(255) NOT NULL,
		                                            Email NVARCHAR(255) NULL,
		                                            Website NVARCHAR(255) NULL,
		                                            Fax NVARCHAR(35) NULL,
		                                            Facilities NVARCHAR(2000),
		                                            RecordStatusId INT NOT NULL,
		                                            CreatedByUserId NVARCHAR(128) NOT NULL,
		                                            CreatedDateTimeUtc DATETIME NOT NULL, 
		                                            ModifiedByUserId NVARCHAR(128) NULL,
		                                            ModifiedDateTimeUtc DATETIME NULL,
		                                            AddressId INT,
		                                            Telephone NVARCHAR(30) NULL,
		                                            BulkUploadVenueId NVARCHAR(255) NULL,
		                                            UKPRN INT NOT NULL,
		                                            AddressLine1 NVARCHAR(110) NULL,
		                                            AddressLine2 NVARCHAR(100) NULL,
		                                            County NVARCHAR(75) NULL,
		                                            Latitude Decimal(9,6) NULL,
		                                            Longitude Decimal(9,6) NULL,
		                                            Postcode NVARCHAR(30) NULL,
		                                            Town NVARCHAR(75) NULL,
		                                            source INT NOT NULL,
		                                            LocationID INT NULL
                                            )
                                            INSERT INTO @Venues
                                            (
		                                            VenueId,
		                                            ProviderId,
		                                            ProviderOwnVenueRef,
		                                            VenueName,
		                                            Email,
		                                            Website,
		                                            Fax,
		                                            Facilities,
		                                            RecordStatusId,
		                                            CreatedByUserId,
		                                            CreatedDateTimeUtc, 
		                                            ModifiedByUserId,
		                                            ModifiedDateTimeUtc,
		                                            AddressId,
		                                            Telephone,
		                                            BulkUploadVenueId,
		                                            UKPRN,
		                                            AddressLine1,
		                                            AddressLine2,
		                                            County,
		                                            Latitude,
		                                            Longitude,
		                                            Postcode,
		                                            Town,
		                                            source,
		                                            LocationID
                                            )
                                            SELECT  distinct Ven.[VenueId],
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
		                                            pr.Ukprn,
                                                    Ad.AddressLine1,
                                                    ad.AddressLine2,
                                                    ad.County,
                                                    ad.[Latitude],
                                                    ad.[Longitude],
		                                            ad.Postcode,
		                                            ad.Town,
		                                            1 as [Source],
		                                            NULL as LocationId
											FROM Venue Ven
                                            INNER JOIN [Address] Ad on Ad.AddressId = Ven.AddressId
                                            INNER JOIN [Provider] pr on pr.ProviderId = ven.ProviderId
                                            WHERE Ven.RecordStatusID = 2
            
											UNION ALL 

											SELECT DISTINCT  0, 
		                                            L.[ProviderId],
		                                            l.ProviderOwnLocationRef,
		                                            L.[LocationName],
		                                            L.[Email],
		                                            L.[Website],
                                                    NULL,
                                                    NULL,
		                                            L.[RecordStatusId],
                                                    L.[CreatedByUserId],
                                                    L.[CreatedDateTimeUtc],
                                                    L.[ModifiedByUserId],
                                                    L.[ModifiedDateTimeUtc],
                                                    L.[AddressId],
                                                    L.[Telephone],
                                                    NULL,
		                                            pr.Ukprn,
                                                    Ad.AddressLine1,
                                                    ad.AddressLine2,
		                                            ad.County,
		                                            ad.[Latitude],
                                                    ad.[Longitude],
                                                    ad.Postcode,
		                                            ad.Town,
		                                            2 as [Source],
		                                            L.LocationId as LocationId
                                            FROM Location l
                                            INNER JOIN Address ad on ad.AddressId = l.AddressId
                                            INNER JOIN Provider pr on pr.ProviderId = l.ProviderId
                                            WHERE l.RecordStatusId = 2

                                            SELECT * FROM @Venues
                                            ";

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                //Read venue
                                venueList.Add(Venue.FromDataReader(dataReader));
                            }

                            // Close the SqlDataReader.
                            dataReader.Close();
                        }

                        sqlConnection.Close();
                    }
                    catch (Exception ex)
                    {
                        log.LogError("An error occured migratiing Venues", ex);
                    }
                }
            }


            foreach (var item in venueList)
            {
                try
                {
                    if (Validate(item))
                    {
                        var cosmosVenue = await GetVenue(item.Source, item.VenueId, item.LocationID, item.UKPRN);
                        if (cosmosVenue != null)
                        {
                            //var s = UriFactory.CreateDocumentUri(databaseId, venuesCollectionId, cosmosVenue.ID.ToString());

                            if (cosmosVenue.UKPRN != item.UKPRN)
                            {
                                continue;
                            }

                            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);
                            var editedVenue = new Dfc.CourseDirectory.Models.Models.Venues.Venue()
                            {
                                ID = cosmosVenue.ID,
                                UKPRN = item.UKPRN,
                                VenueName = item.VenueName,
                                Address1 = item.Address.Address1,
                                Address2 = item.Address.Address2,
                                Town = item.Address.Town,
                                County = item.Address.County,
                                PostCode = item.Address.Postcode,
                                Latitude = item.Address.Latitude,
                                Longitude = item.Address.Longitude,
                                Status = MapVenueStatus(item),
                                UpdatedBy = "VenueMigrator",
                                DateUpdated = DateTime.Now,
                                VenueID = item.VenueId,
                                ProviderID = item.ProviderId,
                                ProvVenueID = item.ProviderOwnVenueRef,
                                Email = item.Email,
                                Website = item.Website,
                                Telephone = item.Telephone,
                                CreatedBy = "VenueMigrator",
                                CreatedDate = DateTime.Now,
                                LocationId = item.LocationID,
                                TribalLocationId = item.LocationID
                            };
                            await _cosmosClient.UpsertDocumentAsync(collectionUri, editedVenue);

                            AddResultMessage(item.UKPRN, item.VenueId, item.LocationID, "Updated Record", $"Old cosmos record LocationId:{cosmosVenue.LocationId}, VenueId: {cosmosVenue.VenueID}");
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
                                County = item.Address.County,
                                PostCode = item.Address.Postcode,
                                Latitude = item.Address.Latitude,
                                Longitude = item.Address.Longitude,
                                Status = MapVenueStatus(item),
                                UpdatedBy = item.CreatedByUserId,
                                DateUpdated = item.CreatedDateTimeUtc,
                                VenueID = item.VenueId,
                                ProviderID = item.ProviderId,
                                ProvVenueID = item.ProviderOwnVenueRef,
                                Email = item.Email,
                                Website = item.Website,
                                Telephone = item.Telephone,
                                CreatedDate = DateTime.Now,
                                CreatedBy = "VenueMigrator",
                                LocationId = item.LocationID,
                                TribalLocationId = item.LocationID
                            };
                            await cosmosDbHelper.CreateDocumentAsync(_cosmosClient, venuesCollectionId, newVenue);

                            //Log that successfully inserted venue
                            AddResultMessage(item.UKPRN, item.VenueId, item.LocationID, "Inserted Venue");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errorMessage = $"An error occured while updating cosmos record for venue {item.VenueId}. {ex.Message}";
                    log.LogError(errorMessage, ex);
                    AddResultMessage(item.UKPRN, item.VenueId, item.LocationID, errorMessage);
                }
            }


            var resultsObjBytes = GetResultAsByteArray(result);
            await WriteResultsToBlobStorage(resultsObjBytes);

            //log completion
            log.LogInformation("Migrating Venues Complete");


            async Task<Dfc.CourseDirectory.Models.Models.Venues.Venue> GetVenue(VenueSource source, int? venueId, int? locationId, int ukprn)
            {
                switch (source)
                {
                    case VenueSource.Venue:
                        return await venueCollectionService.GetDocumentByVenueId(venueId.Value);
                    case VenueSource.Location:
                        return await venueCollectionService.GetDocumentByLocationId(locationId.Value, ukprn);
                    default: return null;
                }
            }

            CourseDirectory.Models.Models.Venues.VenueStatus MapVenueStatus(Venue venue)
            {
                //ignore record status for venues that do not have a postcode & migrate it over
                //as pending.
                if (string.IsNullOrEmpty(venue.Address?.Postcode))
                {
                    return CourseDirectory.Models.Models.Venues.VenueStatus.Pending;
                }

                switch (venue.RecordStatusId)
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

            void AddResultMessage(int ukprn, int venueId, int? locationId, string status, string message = "")
            {
                var validateResult = new ResultMessage() { UKPRN = ukprn, VenueId = venueId, LocationId = locationId, Status = status, Message = message };
                result.Add(validateResult);
            }

            bool Validate(Venue item)
            {
                //are providers on list of whitelisted providers file
                if (!whiteListProviders.Any(x => x == item.UKPRN))
                {
                    AddResultMessage(item.UKPRN, item.VenueId, item.LocationID, "Failed", $"Provider {item.ProviderId} not on whitelist, ukprn {item.UKPRN}");
                    return false;
                }

                if (!item.Address.Latitude.HasValue || !item.Address.Longitude.HasValue)
                {
                    AddResultMessage(item.UKPRN, item.VenueId, item.LocationID, "Skiped", $"Skipped Location because Lat/Long are missing,  {item.ProviderId} not on whitelist, ukprn {item.UKPRN}");
                    return false;
                }
                ////check to see if a record is already held for ukprn
                //if (!ukprnCache.Contains(item.UKPRN))
                //{
                //    var cosmosProvider = await providerCollectionService.ProviderExists(item.UKPRN);
                //    if (!cosmosProvider)
                //    {
                //        AddResultMessage(item.VenueId, item.LocationID, "Failed", "Unknown UKPRN");
                //        return false;
                //    }
                //    else
                //    {
                //        //provider exists - add to cache
                //        ukprnCache.Add(item.UKPRN);
                //    }
                //}

                return true;
            }
        }
    }

    [Serializable()]
    public class ResultMessage
    {
        public int UKPRN { get; set; }
        public int VenueId { get; set; }
        public int? LocationId { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

}

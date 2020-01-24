using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class VenueMigrator
    {
        /// <summary>
        /// This function app migrates venues from a 
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
        public static async Task Run(
                    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] Microsoft.AspNetCore.Http.HttpRequest req,
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

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"SELECT 
	                                              Ven.[VenueId],
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
                                                  Ven.[CosmosId],
                                                  Ven.[AddedByApplicationId],
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
                                            FROM Venue Ven
                                            INNER JOIN [Address] Ad on Ad.AddressId = Ven.AddressId
                                            INNER JOIN [Provider] pr on pr.ProviderId = ven.ProviderId";

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
                                    var newVenue = new Dfc.CourseDirectory.Models.Models.Venues.Venue(Guid.NewGuid().ToString(),
                                                                                                      item.UKPRN,
                                                                                                      item.VenueName,
                                                                                                      item.Address.Address1,
                                                                                                      item.Address.Address2,
                                                                                                      null,
                                                                                                      item.Address.Town,
                                                                                                      item.Address.County,
                                                                                                      item.Address.Postcode,
                                                                                                      item.Address.Latitude,
                                                                                                      item.Address.Longitude,
                                                                                                      MapVenueStatus(item.RecordStatusId),
                                                                                                      item.CreatedByUserId,
                                                                                                      item.CreatedDateTimeUtc,
                                                                                                      item.VenueId,
                                                                                                      item.ProviderId,
                                                                                                      item.ProviderOwnVenueRef
                                                                                                      );
                                    await cosmosDbHelper.CreateDocumentAsync(cosmosDbHelper.GetClient(), venuesCollectionId, newVenue);

                                    //Log that successfully inserted venue
                                    AddResultMessage(item.VenueId, "Success");
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

            CourseDirectory.Models.Models.Venues.VenueStatus MapVenueStatus(int recordStatus)
            {
                switch (recordStatus)
                {
                    case 1: return CourseDirectory.Models.Models.Venues.VenueStatus.Pending;
                    case 2: return CourseDirectory.Models.Models.Venues.VenueStatus.Live;
                    case 3: return CourseDirectory.Models.Models.Venues.VenueStatus.Archived;
                    case 4: return CourseDirectory.Models.Models.Venues.VenueStatus.Deleted;
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

            void AddResultMessage(int venueId, string status, string message="")
            {
                var validateResult = new ResultMessage() {VenueId=venueId, Status = status, Message= message};
                result.Add(validateResult);
            }

            async Task<bool> Validate(Venue item)
            {
                //add prn to cache
                if (!ukprnCache.Contains(item.UKPRN))
                {
                    ukprnCache.Add(item.UKPRN);
                }

                //venue exists
                var cosmosVenue = await venueCollectionService.VenueExists(item.VenueId);
                if (cosmosVenue)
                {
                    AddResultMessage(item.VenueId, "Failed" ,"Venue already exists in Cosmos");
                    return false;
                }

                //check to see if a record is already held for ukprn
                if (!ukprnCache.Contains(item.UKPRN))
                {
                    var cosmosProvider = await providerCollectionService.ProviderExists(item.UKPRN);
                    if (!cosmosProvider)
                    {
                        AddResultMessage(item.VenueId,"Failed", "Unknown UKPRN");
                        return false;
                    }
                }

                //are providers on list of whitelisted providers file
                if (!whiteListProviders.Any(x => x == item.UKPRN))
                {
                    AddResultMessage(item.VenueId, "Failed", $"Provider {item.ProviderId} not on whitelist, ukprn {item.UKPRN}");
                    return false;
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
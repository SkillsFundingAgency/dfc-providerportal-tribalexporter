using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Azure.Documents.Client;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Services.Interfaces;
using Dapper;
using Dfc.CourseDirectory.Services;
using Dfc.CourseDirectory.Services.ApprenticeshipService;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;
using Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Dfc.CourseDirectory.Services.Interfaces.OnspdService;
using Dfc.CourseDirectory.Services.OnspdService;
using Dfc.CourseDirectory.Models.Models.Regions;
using Dfc.CourseDirectory.Models.Models.Venues;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ApprenticeshipMigrator
    {
        [FunctionName(nameof(ApprenticeshipMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper,
                    [Inject] IApprenticeReferenceDataService apprenticeReferenceDataService,
                    [Inject] IApprenticeshipServiceWrapper apprenticeshipService,
                    [Inject] IVenueCollectionService venueCollectionService,
                    [Inject] IOnspdService onspdService

                    )
        {
            var apprenticeshipCollectionId = configuration["CosmosDbCollectionSettings:ApprenticeshipCollectionId"];
            var connectionString = configuration.GetConnectionString("TribalRestore");
            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var whiteListProviders = await GetProviderWhiteList();
            var result = new List<ApprenticeshipResultMessage>();
            var venueExportFileName = $"ApprenticeshipExport-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            const string WHITE_LIST_FILE = "ProviderWhiteList-Apprenticeships.txt";
            var ukprnCache = new List<int>();
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var apprenticeshipList = new List<ApprenticeshipResult>();
            var apprenticeshipErrors = new List<string>();
            var createdBy = "ApprenticeshipMigrator";
            var createdDate = DateTime.Now;

            var apprenticeshipSQL = @"SELECT a.ApprenticeshipId,
	                                           p.ProviderId,
	                                           a.FrameworkCode,
	                                           a.ProgType,
	                                           a.PathwayCode,
	                                           a.StandardCode,
	                                           a.[Version],
	                                           a.MarketingInformation,
	                                           a.[Url],
	                                           a.ContactEmail,
	                                           a.ContactTelephone,
	                                           a.ContactWebsite,
	                                           a.RecordStatusId,
	                                           a.CreatedByUserId,
	                                           a.CreatedDateTimeUtc,
	                                           p.Ukprn
                                        FROM Apprenticeship a
                                        INNER JOIN Provider p on p.ProviderId = a.ProviderId
                                        WHERE a.recordStatusId=2
                                        ORDER BY ProviderId
                                        ";
            var apprenticeshipLocationsSQL = @"SELECT al.ApprenticeshipId,
	                                           al.ApprenticeshipLocationId,
	                                           l.LocationId,
	                                           a.AddressId,
	                                           a.AddressLine1,
	                                           a.AddressLine2,
	                                           a.County,
	                                           a.Postcode,
                                               a.Town,
	                                           a.Longitude,
	                                           a.Latitude,
	                                           l.Website,
	                                           l.Email,
	                                           als.CSV as DeliveryModeStr,
                                               l.Telephone,
	                                           l.LocationName,
	                                           p.ProviderId,
	                                           p.Ukprn,
                                               al.Radius
                                        FROM ApprenticeshipLocation al
                                        INNER JOIN Location l on l.LocationId = al.LocationId
                                        INNER JOIN Provider p on p.ProviderId = l.ProviderId
                                        INNER JOIN Address a ON a.AddressId = l.AddressId
                                        CROSS APPLY (SELECT STRING_AGG(DeliveryModeId,',') as CSV, 
					                                        aldm.ApprenticeshipLocationId
			                                         FROM ApprenticeshipLocationDeliveryMode aldm
		                                             WHERE ApprenticeshipLocationId = al.ApprenticeshipLocationId
			                                         GROUP BY aldm.ApprenticeshipLocationId
			                                         ) als
                                        WHERE al.RecordStatusId = 2 and 
                                              al.ApprenticeshipId = @ApprenticeshipId
                                        ORDER BY ApprenticeshipId,ApprenticeshipLocationId";

            try
            {
                using (var conn1 = new SqlConnection(connectionString))
                using (var apprenticeshipscmd = conn1.CreateCommand())
                {
                    await conn1.OpenAsync();
                    apprenticeshipscmd.CommandText = apprenticeshipSQL;

                    using (var apprenticeshipReader = apprenticeshipscmd.ExecuteReader())
                    {
                        while (await apprenticeshipReader.ReadAsync())
                        {
                            apprenticeshipList.Add(ApprenticeshipResult.FromDataReader(apprenticeshipReader));
                        }
                    }
                }

            }
            catch (Exception e)
            {
                AddResultMessage(0, "Failed", e.Message);
                log.LogError("Error occured Migrating Apprenticeships", e.Message);
            }

            foreach (var item in apprenticeshipList)
            {
                apprenticeshipErrors = new List<string>();
                //check to see if prn is whitelisted
                if (IsOnWhiteList(item.UKPRN))
                {
                    try
                    {
                        var errorList = new List<string>();
                        //get relevant info
                        var exisitingApprenticeship = await GetExistingApprenticeship(item);
                        var referenceDataFramework = await GetReferenceDataFramework(item);
                        var referenceDataStandard = await GetReferenceDataStandard(item);
                        var locations = await GetLocations(item);
                        var cosmosVenues = await GetCosmosVenues(locations);
                        var cosmosProvider = await GetProvider(item);


                        //map objects for creating cosmos record
                        var locs = MapLocations(locations, cosmosVenues);
                        var id = exisitingApprenticeship?.id.ToString() ?? Guid.NewGuid().ToString();
                        var apprenticeType = MapApprenticeshipType(item);
                        var (MappedTitle, mappedNvqLevel) = MapAprenticeshipTitle(item, referenceDataFramework, referenceDataStandard);
                        var mappedStatus = MapApprenticeshipRecordStatus(locs);
                        var mappedApprenticeship = MapApprenticeship(locs,
                                                                    id,
                                                                    item,
                                                                    apprenticeType,
                                                                    mappedStatus,
                                                                    referenceDataFramework?.Id.ToString(),
                                                                    referenceDataStandard?.id.ToString(),
                                                                    cosmosProvider?.id.ToString(),
                                                                    MappedTitle,
                                                                    mappedNvqLevel);

                        //insert record into cosmos
                        await CreateOrUpdateApprenticeshipRecord(mappedApprenticeship);

                        AddResultMessage(item.ApprenticeshipID, "Success", string.Join("\n", apprenticeshipErrors));
                    }
                    catch (Exception e)
                    {
                        AddResultMessage(item.ApprenticeshipID, "Failed", $"Exception occured creating record - {e.Message}");
                        log.LogError("Error occurred creating or updating apprenticeship record!", e);
                    }
                }
                else
                    AddResultMessage(item.ApprenticeshipID, "Skipped", $"PRN {item.UKPRN} not whitelisted");
            }

            //Log Results to blob storage
            var resultsObjBytes = GetResultAsByteArray(result);
            await WriteResultsToBlobStorage(resultsObjBytes);

            //log completion
            log.LogInformation("Migrating Apprenticeships Complete");


            (string, string) MapAprenticeshipTitle(ApprenticeshipResult tribalRecord, ReferenceDataFramework refDataFramework, ReferenceDateStandard refDataStandard)
            {
                var apprenticeshipTitle = tribalRecord.FrameworkCode.HasValue ? refDataFramework?.NasTitle : refDataStandard?.StandardName;
                var nvqLevel2 = refDataStandard?.NotionalEndLevel;

                if (string.IsNullOrEmpty(apprenticeshipTitle))
                    apprenticeshipErrors.Add($"Unable to determine Apprenticeship Title, framework code {tribalRecord.FrameworkCode}, pathway code: {tribalRecord.PathWayCode}, standard code: {tribalRecord.StandardCode}, version: {tribalRecord.Version}");

                return (apprenticeshipTitle, nvqLevel2);
            }

            ApprenticeshipDTO MapApprenticeship(IList<ApprenticeshipLocationDTO> locs, string id, ApprenticeshipResult tribalRecord, ApprenticeshipType apprenticeshipTye,
                RecordStatus recordStatus, string frameworkId, string standardId, string providerId, string apprenticeshipTitle, string notionalNVQLevelv2)
            {
                var cosmosApprenticeship = new ApprenticeshipDTO()
                {
                    id = id,
                    ApprenticeshipId = tribalRecord.ApprenticeshipID,
                    ApprenticeshipTitle = apprenticeshipTitle,
                    ProviderId = providerId,
                    PathWayCode = tribalRecord.PathWayCode,
                    ProgType = tribalRecord.ProgType,
                    ProviderUKPRN = tribalRecord.UKPRN,
                    FrameworkId = frameworkId,
                    StandardId = standardId,
                    FrameworkCode = tribalRecord.FrameworkCode,
                    StandardCode = tribalRecord.StandardCode,
                    Version = tribalRecord.Version,
                    MarketingInformation = tribalRecord.MarketingInformation,
                    Url = tribalRecord.Url,
                    ContactTelephone = tribalRecord.ContactTelephone,
                    ContactEmail = tribalRecord.ContactEmail,
                    ContactWebsite = tribalRecord.ContactWebsite,
                    CreatedBy = createdBy,
                    CreatedDate = createdDate,
                    NotionalNVQLevelv2 = notionalNVQLevelv2,
                    ApprenticeshipLocations = locs,
                    ApprenticeshipType = apprenticeshipTye,
                    RecordStatus = recordStatus
                };
                return cosmosApprenticeship;
            }

            async Task<IList<Dfc.CourseDirectory.Models.Models.Venues.Venue>> GetCosmosVenues(IList<ApprenticeshipLocationResult> locations)
            {
                IList<Dfc.CourseDirectory.Models.Models.Venues.Venue> lst = new List<Dfc.CourseDirectory.Models.Models.Venues.Venue>();
                foreach (var s in locations)
                {
                    var venue = await venueCollectionService.GetDocumentByLocationId(s.LocationId);
                    if (venue != null)
                    {
                        lst.Add(venue);
                    }
                }
                return lst;
            }

            async Task<Provider> GetProvider(ApprenticeshipResult item)
            {
                return await providerCollectionService.GetDocumentByUkprn(item.UKPRN);
            }

            async Task<List<ApprenticeshipLocationResult>> GetLocations(ApprenticeshipResult item)
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    var lst = await sqlConnection.QueryAsync<ApprenticeshipLocationResult>(apprenticeshipLocationsSQL, new { apprenticeshipId = item.ApprenticeshipID }, commandType: CommandType.Text);
                    return lst.ToList();
                }
            }

            async Task<ReferenceDateStandard> GetReferenceDataStandard(ApprenticeshipResult item)
            {
                var apprenticeship = await apprenticeReferenceDataService.GetStandardById(item.StandardCode ?? 0, item.Version ?? 0);
                return apprenticeship?.Value?.Value;
            }

            async Task<ReferenceDataFramework> GetReferenceDataFramework(ApprenticeshipResult item)
            {
                //checks for framework apprenticeship
                var apprenticeship = await apprenticeReferenceDataService.GetFrameworkByCode(item.FrameworkCode ?? 0, item.ProgType ?? 0, item.PathWayCode ?? 0);
                return apprenticeship?.Value?.Value;
            }

            async Task<Apprenticeship> GetExistingApprenticeship(ApprenticeshipResult item)
            {
                //fetch existing apprenticeship row.
                return await apprenticeshipService.GetApprenticeshipByApprenticeshipID(item.ApprenticeshipID);
            }

            async Task CreateOrUpdateApprenticeshipRecord(ApprenticeshipDTO apprenticeship)
            {
                using (var client = cosmosDbHelper.GetClient())
                {
                    var s = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, apprenticeship.id);
                    Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
                    var res = await client.UpsertDocumentAsync(collectionUri, apprenticeship);
                }
            }

            RecordStatus MapApprenticeshipRecordStatus(IList<ApprenticeshipLocationDTO> mappedLocation)
            {
                //if there are any errors with apprenticeshipREcord, set record to migration pending.
                if (apprenticeshipErrors.Any())
                    return RecordStatus.MigrationPending;
                else
                    return RecordStatus.Live;
            }

            //Taken entirely from previous migration logic.
            ApprenticeshipLocationType GetApprenticeshipLocationType(ApprenticeshipLocationResult lo)
            {
                var deliveryModes = lo.DeliveryModes;
                if ((deliveryModes.Contains(1) && !deliveryModes.Contains(2) && deliveryModes.Contains(3)) ||
                    (deliveryModes.Contains(1) && deliveryModes.Contains(2) && !deliveryModes.Contains(3)) ||
                    (deliveryModes.Contains(1) && deliveryModes.Contains(2) && deliveryModes.Contains(3)))
                {
                    return (ApprenticeshipLocationType.ClassroomBasedAndEmployerBased);
                }
                else if ((!deliveryModes.Contains(1) && !deliveryModes.Contains(2) && deliveryModes.Contains(3)) ||
                         (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && !deliveryModes.Contains(3)) ||
                         (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && deliveryModes.Contains(3)))
                {
                    return (ApprenticeshipLocationType.ClassroomBased);
                }
                else if (deliveryModes.Contains(1) && !deliveryModes.Contains(2) && !deliveryModes.Contains(3))
                {
                    return (ApprenticeshipLocationType.EmployerBased);
                }
                else
                {
                    return (ApprenticeshipLocationType.Undefined);
                }
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

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, venueExportFileName, data);
            }

            void AddResultMessage(int apprenticeshipId, string status, string message = "")
            {
                var validateResult = new ApprenticeshipResultMessage() { ApprenticeshipID = apprenticeshipId, Status = status, Message = message };
                result.Add(validateResult);
            }

            byte[] GetResultAsByteArray(IList<ApprenticeshipResultMessage> ob)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<ApprenticeshipResultMessage>(ob);
                    }
                    return memoryStream.ToArray();
                }
            }

            bool IsOnWhiteList(int ukprn)
            {
                if (!whiteListProviders.Any(x => x == ukprn))
                    return false;
                else
                    return true;
            }

            ApprenticeshipType MapApprenticeshipType(ApprenticeshipResult tribalRecord)
            {
                if (tribalRecord.StandardCode.HasValue)
                    return ApprenticeshipType.StandardCode;
                else if (tribalRecord.FrameworkCode.HasValue)
                    return ApprenticeshipType.FrameworkCode;
                else
                {
                    apprenticeshipErrors.Add($"ApprenticeshipId: {tribalRecord.ApprenticeshipID} has undefined apprenticeshipType");
                    return ApprenticeshipType.Undefined;
                }
            }

            IList<ApprenticeshipLocationDTO> MapLocations(IList<ApprenticeshipLocationResult> locations, IList<Dfc.CourseDirectory.Models.Models.Venues.Venue> venues)
            {
                var locationBasedApprenticeshipLocation = new List<ApprenticeshipLocationDTO>();
                var regionBasedApprenticeshipLocation = new List<ApprenticeshipLocationDTO>();

                //no need to proceed
                if (locations == null)
                    return null;


                //employer based apprenticeships - group all locations into regions/subregions
                foreach (var location in locations)
                {
                    var type = GetApprenticeshipLocationType(location);
                    if (type == ApprenticeshipLocationType.EmployerBased)
                    {
                        var allRegionsWithSubRegions = new SelectRegionModel();
                        var onspdRegionSubregion = onspdService.GetOnspdData(new OnspdSearchCriteria(location.Postcode));
                        if (onspdRegionSubregion.IsFailure)
                            apprenticeshipErrors.Add($"LocationId: {location.LocationId} - Querying onspd failed");
                        else if (!onspdRegionSubregion.HasValue)
                        {
                            apprenticeshipErrors.Add($"Location:{location.LocationId} - Did not find a record for postcode: {location.Postcode}");
                            continue;
                        }

                        var selectedSubRegion = allRegionsWithSubRegions.RegionItems.SelectMany(sr => sr.SubRegion.Where(sb =>
                                                                                                sb.SubRegionName == onspdRegionSubregion.Value.Value.LocalAuthority ||
                                                                                                sb.SubRegionName == onspdRegionSubregion.Value.Value.County ||
                                                                                                onspdRegionSubregion.Value.Value.LocalAuthority.Contains(sb.SubRegionName)
                        )).FirstOrDefault();

                        if (selectedSubRegion == null)
                        {
                            apprenticeshipErrors.Add($"Location:{location.LocationId} Unable to match region with ons data api, location skipped");
                            continue;
                        }
                        else
                        {
                            var appLocation = new ApprenticeshipLocationDTO()
                            {
                                Id = Guid.NewGuid().ToString(),
                                VenueId = Guid.Empty.ToString(),
                                TribalId = location.ApprenticeshipLocationId,
                                DeliveryModes = location.DeliveryModes,
                                LocationId = selectedSubRegion.ApiLocationId,
                                Name = location.LocationName,
                                ProviderId = location.ProviderId,
                                ProviderUKPRN = location.UKPRN,
                                Radius = location.Radius,
                                ApprenticeshipLocationType = type,
                                LocationType = LocationType.SubRegion,
                                LocationGuidId = null,
                                Regions = new List<string> { selectedSubRegion.Id },
                                RecordStatus = VenueStatus.Live,
                                CreatedBy = createdBy,
                                CreatedDate = createdDate,
                                UpdatedBy = createdBy,
                                UpdatedDate = createdDate
                            };

                            //region based apprenticeships
                            regionBasedApprenticeshipLocation.Add(appLocation);
                        }
                    }
                    else if (type == ApprenticeshipLocationType.ClassroomBased || type == ApprenticeshipLocationType.ClassroomBasedAndEmployerBased)
                    {
                        //venue based (location based apprenticeships)
                        var cosmosVenueItem = venues.FirstOrDefault(x => x.LocationId == location.LocationId);
                        var status = default(VenueStatus);

                        //set status be that of what the venue status is, otherwise if venue is not found
                        //set status to pending.
                        if (cosmosVenueItem != null)
                            status = cosmosVenueItem.Status;
                        else
                        {
                            apprenticeshipErrors.Add($"LocationId: {location.LocationId} did not find a venue in cosmos, record marked as pending");
                            status = VenueStatus.Pending;
                        }

                        var appLocation = new ApprenticeshipLocationDTO()
                        {
                            Id = Guid.NewGuid().ToString(),
                            VenueId = Guid.Empty.ToString(),
                            TribalId = location.ApprenticeshipLocationId,
                            Address = new Dfc.CourseDirectory.Models.Models.Apprenticeships.Address()
                            {
                                Address1 = cosmosVenueItem?.Address1,
                                Address2 = cosmosVenueItem?.Address2,
                                County = cosmosVenueItem?.County,
                                Email = cosmosVenueItem?.Email,
                                Website = cosmosVenueItem?.Website,
                                Longitude = cosmosVenueItem?.Longitude,
                                Latitude = cosmosVenueItem?.Latitude,
                                Postcode = cosmosVenueItem?.PostCode,
                                Town = cosmosVenueItem?.Town,
                                Phone = cosmosVenueItem?.Telephone
                            },
                            DeliveryModes = location.DeliveryModes,
                            LocationId = location.LocationId,
                            Name = location.LocationName,
                            ProviderId = location.ProviderId,
                            ProviderUKPRN = location.UKPRN,
                            Radius = location.Radius,
                            ApprenticeshipLocationType = type,
                            LocationType = LocationType.Venue,
                            LocationGuidId = cosmosVenueItem?.ID,
                            Regions = null,
                            RecordStatus = status,
                            CreatedBy = createdBy,
                            CreatedDate = createdDate,
                            UpdatedBy = createdBy, 
                            UpdatedDate = createdDate
                        };
                        locationBasedApprenticeshipLocation.Add(appLocation);
                    }
                    else
                    {
                        apprenticeshipErrors.Add($"LocationId: {location.LocationId} skipped as type was unknown {type}");
                        continue;
                    }
                }

                //add a new location with all distinct regions.
                if (regionBasedApprenticeshipLocation.Any(x => x.RecordStatus == VenueStatus.Live))
                {
                    var regionLocation = regionBasedApprenticeshipLocation.FirstOrDefault(x => x.RecordStatus == VenueStatus.Live);
                    regionLocation.Regions = regionBasedApprenticeshipLocation.Where(x => x.Regions != null).SelectMany(x => x.Regions).Distinct().ToList();
                    locationBasedApprenticeshipLocation.Add(regionLocation);
                }

                return locationBasedApprenticeshipLocation;
            }
        }
    }


    public class ApprenticeshipResult
    {
        public int ApprenticeshipID { get; set; }
        public int? FrameworkCode { get; set; }
        public int UKPRN { get; set; }
        public int? StandardCode { get; set; }
        public int? PathWayCode { get; set; }
        public int? ProgType { get; set; }
        public int? Version { get; set; }
        public int ProviderId { get; set; }
        public string MarketingInformation { get; set; }
        public string Url { get; set; }
        public string ContactEmail { get; set; }
        public string ContactTelephone { get; set; }
        public string ContactWebsite { get; set; }
        public int RecordStatusId { get; set; }
        public string CreatedByUserId { get; set; }
        public DateTime CreatedDateTimeUtc { get; set; }


        public static ApprenticeshipResult FromDataReader(SqlDataReader reader)
        {
            var result = new ApprenticeshipResult()
            {
                ApprenticeshipID = (int)reader["ApprenticeshipID"],
                FrameworkCode = reader["Frameworkcode"] as int?,
                UKPRN = (int)reader["UKPRN"],
                PathWayCode = reader["PathWayCode"] as int?,
                ProgType = reader["ProgType"] as int?,
                Version = reader["Version"] as int?,
                StandardCode = reader["StandardCode"] as int?,
                ProviderId = (int)reader["ProviderId"],
                MarketingInformation = reader["MarketingInformation"] as string,
                Url = reader["URL"] as string,
                ContactEmail = reader["ContactEmail"] as string,
                ContactTelephone = reader["ContactTelephone"] as string,
                ContactWebsite = reader["ContactWebsite"] as string,
                RecordStatusId = (int)reader["RecordStatusId"],
                CreatedByUserId = reader["CreatedByUserId"] as string,
                CreatedDateTimeUtc = (DateTime)reader["CreatedDateTimeUtc"]
            };
            return result;
        }
    }

    public class ApprenticeshipLocationResult
    {
        public int ApprenticeshipLocationId { get; set; }
        public int LocationId { get; set; }
        public int AddressId { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string County { get; set; }
        public string Postcode { get; set; }
        public string Town { get; set; }
        public string Telephone { get; set; }
        public string LocationName { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string Website { get; set; }
        public string Email { get; set; }
        public string DeliveryModeStr { get; set; }
        public int UKPRN { get; set; }
        public int ProviderId { get; set; }
        public int? Radius { get; set; }
        public List<int> DeliveryModes => DeliveryModeStr.Split(',').Select(x => Convert.ToInt32(x)).ToList();
    }

    [Serializable()]
    public class ApprenticeshipResultMessage
    {
        public int ApprenticeshipID { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class ApprenticeshipDTO
    {
        public string id { get; set; }
        public int ApprenticeshipId { get; set; }
        public string ApprenticeshipTitle { get; set; }
        public string ProviderId { get; set; }
        public string StandardId { get; set; }
        public string FrameworkId { get; set; }
        public int? FrameworkCode { get; set; }
        public int ProviderUKPRN { get; set; }
        public int? StandardCode { get; set; }
        public int? PathWayCode { get; set; }
        public int? ProgType { get; set; }
        public int? Version { get; set; }
        public string MarketingInformation { get; set; }
        public string Url { get; set; }
        public string ContactEmail { get; set; }
        public string ContactTelephone { get; set; }
        public string ContactWebsite { get; set; }
        public RecordStatus RecordStatus { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public IList<ApprenticeshipLocationDTO> ApprenticeshipLocations { get; set; }
        public string NotionalNVQLevelv2 { get; set; }
        public ApprenticeshipType ApprenticeshipType { get; set; }
    }

    public class ApprenticeshipLocationDTO
    {
        public string Id { get; set; }
        public string VenueId { get; set; }
        public List<int> DeliveryModes { get; set; }
        public CourseDirectory.Models.Models.Apprenticeships.Address Address { get; set; }
        public int? LocationId { get; set; }
        public string Name { get; set; }
        public int ProviderUKPRN { get; set; }
        public int ProviderId { get; set; }
        public ApprenticeshipLocationType ApprenticeshipLocationType { get; set; }
        public LocationType LocationType { get; set; }
        public List<string> Regions { get; set; }
        public int? Radius { get; set; }
        public VenueStatus RecordStatus { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public string BulkUploadErrors { get; set; }
        public int TribalId { get; set; }
        public string LocationGuidId { get; set; }
    }
}
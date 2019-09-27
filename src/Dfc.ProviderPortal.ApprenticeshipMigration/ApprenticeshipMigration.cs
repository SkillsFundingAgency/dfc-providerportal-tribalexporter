using Dfc.CourseDirectory.Common.Interfaces;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Interfaces.Providers;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.CourseDirectory.Models.Models.Regions;
using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.CourseDirectory.Services.ApprenticeshipService;
using Dfc.CourseDirectory.Services.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using Dfc.CourseDirectory.Services.ProviderService;
using Dfc.CourseDirectory.Services.VenueService;
using Dfc.ProviderPortal.ApprenticeshipMigration.Helpers;
using Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces;
using Dfc.ProviderPortal.ApprenticeshipMigration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Services.Interfaces.OnspdService;
using Dfc.CourseDirectory.Services.OnspdService;
using Providercontact = Dfc.ProviderPortal.ApprenticeshipMigration.Models.Providercontact;

namespace Dfc.ProviderPortal.ApprenticeshipMigration
{
    public class ApprenticeshipMigration : IApprenticeshipMigration
    {
        private readonly IVenueService _venueService;
        private readonly IProviderService _providerService;
        private readonly IApprenticeReferenceDataService _apprenticeReferenceDataService;
        private readonly IApprenticeshipService _apprenticeshipService;
        private readonly IBlobStorageService _blobService;
        private readonly ILogger logger;
        private readonly ApprenticeshipMigrationSettings _settings;
        private readonly IOnspdService _onspdService;


        public ApprenticeshipMigration(BlobStorageServiceResolver blobStorageServiceResolver,
            IOptions<ApprenticeshipMigrationSettings> settings,
            IVenueService venueService, IProviderService providerService,
            IApprenticeReferenceDataService apprenticeReferenceDataService,
            IApprenticeshipService apprenticeshipService, IOnspdService onspdService)
        {
            _venueService = venueService;
            _providerService = providerService;
            _apprenticeReferenceDataService = apprenticeReferenceDataService;
            _apprenticeshipService = apprenticeshipService;
            _onspdService = onspdService;
            _settings = settings.Value;
            _blobService = blobStorageServiceResolver(nameof(ApprenticeshipMigration));
        }
        public async Task RunApprenticeShipMigration(ILogger logger)
        {
            logger.LogInformation("Starting the Apprenticeship Migration Process");

            

            string adminReport = "                         Admin Report " + Environment.NewLine;
            adminReport += "________________________________________________________________________________" + Environment.NewLine + Environment.NewLine;


            int courseTransferId = 0;
            bool goodToTransfer = false;
            TransferMethod transferMethod = TransferMethod.Undefined;
            int? singleProviderUKPRN = null;
            string bulkUploadFileName = string.Empty;



            logger.LogInformation("The Migration Tool is running in Blob Mode." + Environment.NewLine + "Please, do not close this window until \"Migration completed\" message is displayed." + Environment.NewLine);

            string errorMessageGetCourses = string.Empty;
            var providerUKPRNList =  await _blobService.GetBulkUploadProviderListFile(_settings.MigrationWindow);
            if (providerUKPRNList == null)
            {
                throw new Exception("Unable to retrieve providers via blob storage.");
            }
            else
            {
                logger.LogInformation($"Migrating {providerUKPRNList.Count()} Apprenticeship provider(s)");
                goodToTransfer = true;
                transferMethod = TransferMethod.CourseMigrationTool;
            }


            Stopwatch adminStopWatch = new Stopwatch();
            adminStopWatch.Start();
            Stopwatch provStopWatch = new Stopwatch();
            provStopWatch.Start();

            int CountProviders = 0;
            int CountAllApprenticeships = 0;
            int CountAllApprenticeshipPending = 0;
            int CountAllApprenticeshipLive = 0;
            int CountAllApprenticeshipLocations = 0;
            int CountAllApprenticeshipLocationsPending = 0;
            int CountAllApprenticeshipLocationsLive = 0;
            int CountAllUnknownStandardsOrFrameworks = 0;

            logger.LogInformation($"Migrating {providerUKPRNList.Count()} Provider's Apprenticeships");
            foreach (var providerUKPRN in providerUKPRNList)
            {
                Stopwatch providerStopWatch = new Stopwatch();
                providerStopWatch.Start();

                CountProviders++;

                int CountApprenticeships = 0;
                int CountApprenticeshipPending = 0;
                int CountApprenticeshipLive = 0;
                int CountApprenticeshipLocations = 0;
                int CountApprenticeshipLocationsPending = 0;
                int CountApprenticeshipLocationsLive = 0;
                int CountAppreticeshipFailedToMigrate = 0;
                int CountUnknownStandardsOrFrameworks = 0;

                string providerReport = "                         Migration Report " + Environment.NewLine;

                // GetProviderDetailsByUKPRN
                string errorMessageGetProviderDetailsByUKPRN = string.Empty;
                var provider = DataHelper.GetProviderDetailsByUKPRN(providerUKPRN, _settings.ConnectionString, out errorMessageGetProviderDetailsByUKPRN);
                var ProviderGuidId = new Guid();
                var TribalProviderId = provider.ProviderId;
                string providerUkprnLine = "Provider - " + providerUKPRN + " - " + provider.ProviderName;
                Console.WriteLine(providerUkprnLine);
                adminReport += "_________________________________________________________________________________________________________" + Environment.NewLine;
                adminReport += Environment.NewLine + providerUkprnLine + Environment.NewLine;

                if (!string.IsNullOrEmpty(errorMessageGetProviderDetailsByUKPRN))
                {
                    logger.LogError($"Unable to get provider: {providerUKPRN}, error: {errorMessageGetProviderDetailsByUKPRN}");
                    adminReport += $"* ATTENTION * { errorMessageGetProviderDetailsByUKPRN }" + Environment.NewLine;
                }
                else
                {
                    var providerCriteria = new ProviderSearchCriteria(providerUKPRN.ToString());
                    var providerResult = await _providerService.GetProviderByPRNAsync(providerCriteria);

                    if (providerResult.IsSuccess && providerResult.HasValue)
                    {
                        var providers = providerResult.Value.Value;
                        if (providers.Count().Equals(1))
                        {
                            var providerToUpdate = providers.FirstOrDefault();
                            ProviderGuidId = providerToUpdate.id; // We need our Provider GUID id

                            #region  Update Provider

                            await UpdateProviderType(_providerService, providerToUpdate);

                            if (_settings.UpdateProvider)
                            {
                                // Commented out fields are not updated
                                //providerToUpdate.ProviderName = provider.ProviderName;
                                //providerToUpdate.TradingName = provider.TradingName;
                                providerToUpdate.ProviderId = provider.ProviderId;
                                providerToUpdate.UPIN = provider.UPIN;
                                providerToUpdate.MarketingInformation = provider.MarketingInformation;

                                var ApprenticeshipProviderContact = new Providercontact();
                                ApprenticeshipProviderContact.ContactType = "A";
                                ApprenticeshipProviderContact.ContactTelephone1 = provider.Telephone;
                                ApprenticeshipProviderContact.ContactEmail = provider.Email;
                                ApprenticeshipProviderContact.ContactWebsiteAddress = provider.Website;
                                ApprenticeshipProviderContact.LastUpdated = DateTime.Now;
                                if (providerToUpdate.ProviderContact == null)
                                {
                                    providerToUpdate.ProviderContact = new IProvidercontact[] { ApprenticeshipProviderContact };
                                }
                                else
                                {
                                    providerToUpdate.ProviderContact = providerToUpdate.ProviderContact.Append(ApprenticeshipProviderContact).ToArray();
                                }

                                // Call ProviderService API to update provider
                                var updateProviderResult = await _providerService.UpdateProviderDetails(providerToUpdate);
                                if (updateProviderResult.IsSuccess)
                                {
                                    adminReport += $"Provider ( { providerToUpdate.ProviderName } ) with UKPRN ( { providerToUpdate.UnitedKingdomProviderReferenceNumber } ) was updated in CosmosDB" + Environment.NewLine;
                                }
                                else
                                {
                                    var errorMessage =
                                        $"*** ATTENTION *** - Problem with the ProviderService - UpdateProviderDetails For provider: {providerUKPRN}-  Error:  {updateProviderResult?.Error}";
                                    logger.LogError(errorMessage);
                                    adminReport += errorMessage + Environment.NewLine;
                                }

                            }

                            #endregion
                        }
                        else
                        {
                            var errorMessage = $"We CANNOT IDENTIFY the Provider - {providerUKPRN}";
                            logger.LogError(errorMessage);
                            providerReport += errorMessage + Environment.NewLine + Environment.NewLine;
                        }
                    }
                    else
                    {
                        var errorMessage = $"ERROR on GETTING the Provider - { providerResult.Error }";
                        providerReport += errorMessage + Environment.NewLine + Environment.NewLine;
                        logger.LogError(errorMessage);
                    }

                    if (_settings.DeleteCoursesByUKPRN)
                    {
                        providerReport += $"ATTENTION - Existing Courses for Provider '{ provider.ProviderName }' with UKPRN  ( { providerUKPRN } ) to be deleted." + Environment.NewLine;

                        // Call the service 
                        var deleteCoursesByUKPRNResult = await _apprenticeshipService.DeleteApprenticeshipsByUKPRNAsync(providerUKPRN);

                        if (deleteCoursesByUKPRNResult.IsSuccess && deleteCoursesByUKPRNResult.HasValue)
                        {
                            providerReport += $"The deleted courses:  " + Environment.NewLine;
                            // StatusCode => NoContent = 204 is good 
                            foreach (var deleteMessage in deleteCoursesByUKPRNResult.Value)
                            {
                                providerReport += deleteMessage + Environment.NewLine;
                            }
                        }
                        else
                        {
                            logger.LogError("ERROR: Failed to delete provider current courses for: " + providerUKPRN);
                            providerReport += $"Error on deleteing courses -  { deleteCoursesByUKPRNResult.Error }  " + Environment.NewLine;
                        }
                    }

                    // Get Apprenticeships by ProviderId                    
                    string errorMessageGetApprenticeshipsByProviderId = string.Empty;
                    var apprenticeships = DataHelper.GetApprenticeshipsByProviderId(provider.ProviderId ?? 0, _settings.ConnectionString, out errorMessageGetApprenticeshipsByProviderId);
                    if (!string.IsNullOrEmpty(errorMessageGetApprenticeshipsByProviderId))
                    {
                        var errorMessage = $"* ATTENTION * provider {providerUKPRN} Error :{errorMessageGetApprenticeshipsByProviderId}";
                        adminReport += errorMessage + Environment.NewLine;
                        logger.LogError(errorMessage);
                    }
                    else
                    {
                        CountApprenticeships = apprenticeships.Count;

                        logger.LogInformation($"Migrating {apprenticeships.Count} for provider {providerUKPRN}");


                        foreach (var apprenticeship in apprenticeships)
                        {
                            logger.LogInformation($"app : {apprenticeship.ApprenticeshipId}");
                            logger.Log(LogLevel.Information, "test");
                            int CountApprenticeshipLocationsPerAppr = 0;
                            int CountApprenticeshipLocationsPendingPerAppr = 0;
                            int CountApprenticeshipLocationsLivePerAppr = 0;
                            var isValidApprenticeship = true;

                            // // Mapp Apprenticeships
                            apprenticeship.id = Guid.NewGuid();
                            apprenticeship.ProviderId = ProviderGuidId;
                            apprenticeship.ProviderUKPRN = providerUKPRN;
                            apprenticeship.TribalProviderId = TribalProviderId;

                            apprenticeship.CreatedDate = DateTime.Now;
                            apprenticeship.CreatedBy = "DFC – Apprenticeship Migration Tool";
                            adminReport += "____________________________________________________________________" + Environment.NewLine;
                            // Get Framework/Standard GUID id => ???? Call ReferenceData Service
                            if (apprenticeship.FrameworkCode.HasValue && apprenticeship.ProgType.HasValue && apprenticeship.PathwayCode.HasValue)
                            {
                                apprenticeship.ApprenticeshipType = ApprenticeshipType.FrameworkCode;
                                var framework = await _apprenticeReferenceDataService.GetFrameworkByCode(apprenticeship.FrameworkCode.Value,
                                    apprenticeship.ProgType.Value, apprenticeship.PathwayCode.Value);
                                if (framework.HasValue)
                                {
                                    apprenticeship.ApprenticeshipType = ApprenticeshipType.FrameworkCode;
                                    adminReport += $"> Framework Apprenticeship - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } )" + Environment.NewLine;
                                    apprenticeship.ApprenticeshipTitle = framework.Value.Value.NasTitle;
                                    apprenticeship.FrameworkId = framework.Value.Value.Id;
                                }
                                else
                                {
                                    apprenticeship.ApprenticeshipType = ApprenticeshipType.Undefined;
                                    isValidApprenticeship = false;
                                    CountUnknownStandardsOrFrameworks++;
                                    adminReport += $"> * ATTENTION * Apprenticeship NOT Defined - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } ), StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;
                                }

                            }
                            else if (apprenticeship.StandardCode.HasValue && apprenticeship.Version.HasValue)
                            {
                                apprenticeship.ApprenticeshipType = ApprenticeshipType.StandardCode;
                                var standard = await _apprenticeReferenceDataService.GetStandardById(apprenticeship.StandardCode.Value, apprenticeship.Version.Value);
                                if (standard.HasValue)
                                {
                                    apprenticeship.ApprenticeshipType = ApprenticeshipType.StandardCode;
                                    apprenticeship.ApprenticeshipTitle = standard.Value.Value.StandardName;
                                    apprenticeship.StandardId = standard.Value.Value.id;
                                    apprenticeship.NotionalNVQLevelv2 = standard.Value.Value.NotionalEndLevel;
                                    adminReport += $"> Standard Apprenticeship - StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;

                                }
                                else
                                {
                                    apprenticeship.ApprenticeshipType = ApprenticeshipType.Undefined;
                                    isValidApprenticeship = false;
                                    CountUnknownStandardsOrFrameworks++;
                                    adminReport += $"> * ATTENTION * Apprenticeship NOT Defined - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } ), StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;
                                }
                            }
                            else
                            {
                                apprenticeship.ApprenticeshipType = ApprenticeshipType.Undefined;
                                CountUnknownStandardsOrFrameworks++;
                                isValidApprenticeship = false;
                                adminReport += $"> * ATTENTION * Apprenticeship NOT Defined - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } ), StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;
                            }

                            // Get ApprenticeshipLocations                          
                            string errorMessageGetApprenticeshipLocations = string.Empty;
                            var apprenticeshipLocations = DataHelper.GetApprenticeshipLocationsByApprenticeshipId(apprenticeship.ApprenticeshipId ?? 0, _settings.ConnectionString, out errorMessageGetApprenticeshipLocations);
                            if (!string.IsNullOrEmpty(errorMessageGetApprenticeshipLocations))
                            {
                                adminReport += $"* ATTENTION * { errorMessageGetApprenticeshipLocations }" + Environment.NewLine;
                            }
                            else
                            {
                                var locationBasedApprenticeshipLocation = new List<ApprenticeshipLocation>();
                                var regionBasedApprenticeshipLocation = new List<ApprenticeshipLocation>();

                                foreach (var apprenticeshipLocation in apprenticeshipLocations)
                                {
                                    apprenticeshipLocation.Id = Guid.NewGuid();
                                    apprenticeshipLocation.RecordStatus = RecordStatus.Live;
                                    apprenticeshipLocation.CreatedDate = DateTime.Now;
                                    apprenticeshipLocation.CreatedBy = "DFC – Apprenticeship Migration Tool";
                                    apprenticeshipLocation.ProviderUKPRN = apprenticeship.ProviderUKPRN;
                                    apprenticeshipLocation.ProviderId = apprenticeship.TribalProviderId;

                                    adminReport += "__________________________" + Environment.NewLine;
                                    adminReport += $">>> ApprenticeshipLocation with TribalLocationId ( { apprenticeshipLocation.LocationId } )";

                                    // Get ApprenticeshipLocation DeliveryModes
                                    string errorMessageGetDeliveryModes = string.Empty;
                                    var deliveryModes = DataHelper.GetDeliveryModesByApprenticeshipLocationId(apprenticeshipLocation.ApprenticeshipLocationId, _settings.ConnectionString, out errorMessageGetDeliveryModes);
                                    if (!string.IsNullOrEmpty(errorMessageGetDeliveryModes))
                                    {
                                        adminReport += Environment.NewLine + $"* ATTENTION * { errorMessageGetDeliveryModes }" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        apprenticeshipLocation.DeliveryModes = deliveryModes;
                                        // Mapp DeliveryModes to ApprenticeshipLocationType
                                        // 1 - 100PercentEmployer, 2 - DayRelease, 3 - BlockRelease
                                        if ((deliveryModes.Contains(1) && !deliveryModes.Contains(2) && deliveryModes.Contains(3)) ||
                                           (deliveryModes.Contains(1) && deliveryModes.Contains(2) && !deliveryModes.Contains(3)) ||
                                           (deliveryModes.Contains(1) && deliveryModes.Contains(2) && deliveryModes.Contains(3)))
                                        {
                                            apprenticeshipLocation.ApprenticeshipLocationType = ApprenticeshipLocationType.ClassroomBasedAndEmployerBased;
                                            apprenticeshipLocation.LocationType = LocationType.Venue;
                                            apprenticeshipLocation.Radius = _settings.VenueBasedRadius; // Leave it as it is. COUR-419
                                            adminReport += $" - ApprenticeshipLocationType ( { apprenticeshipLocation.ApprenticeshipLocationType } )" + Environment.NewLine;
                                        }
                                        else if ((!deliveryModes.Contains(1) && !deliveryModes.Contains(2) && deliveryModes.Contains(3)) ||
                                                 (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && !deliveryModes.Contains(3)) ||
                                                 (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && deliveryModes.Contains(3)))
                                        {
                                            apprenticeshipLocation.ApprenticeshipLocationType = ApprenticeshipLocationType.ClassroomBased;
                                            apprenticeshipLocation.LocationType = LocationType.Venue;
                                            apprenticeshipLocation.Radius = _settings.VenueBasedRadius;
                                            adminReport += $" -  ApprenticeshipLocationType ( { apprenticeshipLocation.ApprenticeshipLocationType } )" + Environment.NewLine;
                                        }
                                        else if (deliveryModes.Contains(1) && !deliveryModes.Contains(2) && !deliveryModes.Contains(3))
                                        {
                                            apprenticeshipLocation.ApprenticeshipLocationType = ApprenticeshipLocationType.EmployerBased;
                                            // apprenticeshipLocation.LocationType = Region or SubRegion depending ... bellow;
                                            adminReport += $" -  ApprenticeshipLocationType ( { apprenticeshipLocation.ApprenticeshipLocationType } )" + Environment.NewLine;
                                        }
                                        else
                                        {
                                            apprenticeshipLocation.ApprenticeshipLocationType = ApprenticeshipLocationType.Undefined;
                                            apprenticeshipLocation.LocationType = LocationType.Undefined;
                                            apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                            adminReport += Environment.NewLine + $"*** ATTENTION *** ApprenticeshipLocationType ( { apprenticeshipLocation.ApprenticeshipLocationType } )" + Environment.NewLine;
                                        }

                                        // Get Location by Tribal LocationId
                                        string errorMessageGetTribalLocation = string.Empty;
                                        var location = DataHelper.GetLocationByLocationIdPerProvider(apprenticeshipLocation.LocationId ?? 0, provider.ProviderId ?? 0, _settings.ConnectionString, out errorMessageGetTribalLocation);
                                        if (!string.IsNullOrEmpty(errorMessageGetTribalLocation))
                                        {
                                            apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                            adminReport += Environment.NewLine + $"*** ATTENTION *** { errorMessageGetTribalLocation }" + Environment.NewLine;
                                        }
                                        else
                                        {
                                            if (location == null)
                                            {
                                                apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                adminReport +=
                                                    $"We couldn't get location for LocationId ({apprenticeshipLocation.LocationId}) " +
                                                    Environment.NewLine;
                                            }
                                            else
                                            {
                                                apprenticeshipLocation.Name = location.LocationName;
                                                apprenticeshipLocation.Address = new Address
                                                {
                                                    Address1 = location.AddressLine1,
                                                    Address2 = location.AddressLine2,
                                                    County = location.County,
                                                    Email = location.Email,
                                                    Latitude = double.Parse(location.Longitude.ToString()),
                                                    Longitude = double.Parse(location.Longitude.ToString()),
                                                    Phone = location.Telephone,
                                                    Postcode = location.Postcode,
                                                    Town = location.Town,
                                                    Website = location.Website
                                                };



                                                // Venue Locations
                                                if ((apprenticeshipLocation.ApprenticeshipLocationType.Equals(
                                                        ApprenticeshipLocationType.ClassroomBased)) ||
                                                    apprenticeshipLocation.ApprenticeshipLocationType.Equals(
                                                        ApprenticeshipLocationType.ClassroomBasedAndEmployerBased))
                                                {
                                                    #region Venue Locations

                                                    GetVenuesByPRNAndNameCriteria venueCriteria =
                                                        new GetVenuesByPRNAndNameCriteria(
                                                            apprenticeship.ProviderUKPRN.ToString(),
                                                            location.LocationName);
                                                    var venuesResult =
                                                        await _venueService.GetVenuesByPRNAndNameAsync(venueCriteria);

                                                    if (venuesResult.IsSuccess && venuesResult.Value.Value != null)
                                                    {
                                                        var venues = venuesResult.Value.Value;

                                                        // It is a good case, but ... 
                                                        if (venues.Count().Equals(1))
                                                        {
                                                            var venue = venues.FirstOrDefault();

                                                            bool UpdateVenue = false;
                                                            if (venue.LocationId == null || venue.LocationId.Equals(0))
                                                            {
                                                                // We don't have valid LocationId assigned by our VenueService API
                                                                apprenticeshipLocation.RecordStatus =
                                                                    RecordStatus.MigrationPending;
                                                                adminReport +=
                                                                    $"*** ATTENTION *** We don't have valid LocationId assigned by our VenueService API - Location/VenueName ({location.LocationName}). " +
                                                                    Environment.NewLine;
                                                            }
                                                            else
                                                            {
                                                                apprenticeshipLocation.LocationId = venue.LocationId;
                                                                apprenticeshipLocation.LocationGuidId =
                                                                    new Guid(venue.ID);
                                                                apprenticeshipLocation.Address = new Address()
                                                                {
                                                                    Address1 = venue.Address1,
                                                                    Address2 = venue.Address2,
                                                                    County = venue.County,
                                                                    Email = venue.Email,
                                                                    Latitude = double.Parse(
                                                                        venue.Latitude.ToString(CultureInfo
                                                                            .InvariantCulture)),
                                                                    Longitude = double.Parse(
                                                                        venue.Longitude.ToString(CultureInfo
                                                                            .InvariantCulture)),
                                                                    Phone = venue.PHONE,
                                                                    Postcode = venue.PostCode,
                                                                    Town = venue.Town,
                                                                    Website = venue.Website
                                                                };


                                                                if (venue.Status.Equals(VenueStatus.Live))
                                                                {
                                                                    // Check Venue.TribalLocationId is equal to location.LocationId
                                                                    if (venue.TribalLocationId == location.LocationId)
                                                                    {
                                                                        // It's good match - we don't update Contacts as we asume that they were updated when venue was initially created from the same location
                                                                        adminReport +=
                                                                            $"It's a good match " + Environment.NewLine;
                                                                    }
                                                                    else
                                                                    {
                                                                        // If it's different we will check whether Venue is TribalLocation. 
                                                                        if (venue.TribalLocationId != null ||
                                                                            venue.TribalLocationId.Equals(0))
                                                                        {
                                                                            // Venue is also TribalLocation. But it was used previosly. Therefor we do NOT update anything. Just use it. (These overides AL - TribalLocation relation.)  
                                                                            adminReport +=
                                                                                $"Venue is also TribalLocation. But it was used previosly. Therefor we do NOT update anything. Just use it." +
                                                                                Environment.NewLine;
                                                                        }
                                                                        else
                                                                        {
                                                                            // Venue is NOT TribalLocation. We can use it but we have to update Contacts.           
                                                                            adminReport +=
                                                                                $"Venue is NOT TribalLocation. We can use it but we have to update Contacts." +
                                                                                Environment.NewLine;
                                                                            UpdateVenue = true;
                                                                            venue.PHONE = location.Telephone;
                                                                            venue.EMAIL = location.Email;
                                                                            venue.WEBSITE = location.Website;
                                                                            venue.TribalLocationId =
                                                                                location.LocationId;
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // TODO _ CHECK IT OUT
                                                                    // Venue has Status ( {venue.Status} ). We will bring it to LIVE and update Contacts.
                                                                    adminReport +=
                                                                        $"Venue has Status ( {venue.Status} ). We will bring it to LIVE and update Contacts." +
                                                                        Environment.NewLine;

                                                                    UpdateVenue = true;
                                                                    // Update Venue.Status
                                                                    venue.Status = VenueStatus.Live;
                                                                    // Update Contacts (Telephone, Email, Website) 
                                                                    venue.PHONE = location.Telephone;
                                                                    venue.EMAIL = location.Email;
                                                                    venue.WEBSITE = location.Website;
                                                                    venue.TribalLocationId = location.LocationId;
                                                                }
                                                            }

                                                            if (UpdateVenue)
                                                            {

                                                                var updateVenueResult =
                                                                    await _venueService.UpdateAsync(venue);
                                                                if (updateVenueResult.IsSuccess &&
                                                                    updateVenueResult.HasValue)
                                                                {
                                                                    adminReport +=
                                                                        $"Venue ( {venue.VenueName} ) was updated in CosmosDB" +
                                                                        Environment.NewLine;
                                                                }
                                                                else
                                                                {
                                                                    // Problem with the service  - UpdateAsync -  Error:  { updateVenueResult?.Error }
                                                                    apprenticeshipLocation.RecordStatus =
                                                                        RecordStatus.MigrationPending;
                                                                    adminReport +=
                                                                        $"Problem with the service - UpdateAsync -  Error:  {updateVenueResult?.Error}" +
                                                                        Environment.NewLine;
                                                                }
                                                            }
                                                        }
                                                        else if (venues.Count().Equals(0))
                                                        {
                                                            var venueID = Guid.NewGuid();
                                                            // There is no such Venue - Add it
                                                            var addVenue = new Venue(venueID.ToString(),
                                                                location.ProviderUKPRN,
                                                                location.LocationName,
                                                                location.AddressLine1,
                                                                location.AddressLine2,
                                                                null,
                                                                location.Town,
                                                                location.County,
                                                                location.Postcode,
                                                                location.Latitude,
                                                                location.Longitude,
                                                                VenueStatus.Live,
                                                                "DFC – Apprenticeship Migration Tool",
                                                                DateTime.Now);
                                                            addVenue.UKPRN = providerUKPRN;
                                                            addVenue.PHONE = location.Telephone;
                                                            addVenue.EMAIL = location.Email;
                                                            addVenue.WEBSITE = location.Website;
                                                            addVenue.TribalLocationId = location.LocationId;

                                                            apprenticeshipLocation.LocationId = location.LocationId;
                                                            apprenticeshipLocation.LocationGuidId = venueID;
                                                            apprenticeshipLocation.Address = new Address()
                                                            {
                                                                Address1 = location.AddressLine1,
                                                                Address2 = location.AddressLine2,
                                                                County = location.County,
                                                                Email = location.Email,
                                                                Latitude = double.Parse(
                                                                    location.Latitude.ToString(CultureInfo
                                                                        .InvariantCulture)),
                                                                Longitude = double.Parse(
                                                                    location.Longitude.ToString(CultureInfo
                                                                        .InvariantCulture)),
                                                                Phone = location.Telephone,
                                                                Postcode = location.Postcode,
                                                                Town = location.Town,
                                                                Website = location.Website
                                                            };

                                                            adminReport +=
                                                                $"Adds Venue for LocationName ({location.LocationName})" +
                                                                Environment.NewLine;

                                                            if (_settings.GenerateJsonFilesLocally)
                                                            {
                                                                var addVenueJson =
                                                                    JsonConvert.SerializeObject(addVenue);
                                                                string jsonFileName = string.Format(
                                                                    "{0}-Venue-{1}-{2}.json",
                                                                    DateTime.Now.ToString("yyMMdd-HHmmss"),
                                                                    providerUKPRN,
                                                                    location.LocationName.Replace(" ", string.Empty)
                                                                        .Replace(@"\", string.Empty)
                                                                        .Replace("/", string.Empty));
                                                                string AddVenuePath = string.Format(@"{0}\AddVenue",
                                                                    _settings.JsonApprenticeshipFilesPath);
                                                                if (!Directory.Exists(AddVenuePath))
                                                                    Directory.CreateDirectory(AddVenuePath);
                                                                File.WriteAllText(
                                                                    string.Format(@"{0}\{1}", AddVenuePath,
                                                                        jsonFileName), addVenueJson);
                                                            }
                                                            else
                                                            {
                                                                var addVenueResult =
                                                                    await _venueService.AddAsync(addVenue);
                                                                if (addVenueResult.IsSuccess && addVenueResult.HasValue)
                                                                {
                                                                    // All good => It's important to update apprenticeshipLocation.LocationId
                                                                    apprenticeshipLocation.LocationId =
                                                                        ((Venue)addVenueResult.Value).LocationId;
                                                                    adminReport +=
                                                                        $"All good => It's important to update apprenticeshipLocation.LocationId" +
                                                                        Environment.NewLine;
                                                                }
                                                                else
                                                                {
                                                                    // Problem with the service Error:  { addVenueResult?.Error }
                                                                    apprenticeshipLocation.RecordStatus =
                                                                        RecordStatus.MigrationPending;
                                                                    adminReport +=
                                                                        $"Problem with the service - AddAsync -  Error:  {addVenueResult?.Error}" +
                                                                        Environment.NewLine;
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // We have multiple Venues for the same name ({ location.LocationName }).  Issue raised many times
                                                            adminReport +=
                                                                $"Multiple Venues for the same name ({location.LocationName}). " +
                                                                Environment.NewLine;
                                                            apprenticeshipLocation.RecordStatus =
                                                                RecordStatus.MigrationPending;
                                                        }
                                                    }
                                                    else
                                                    {

                                                        // Problem with the service GetVenuesByPRNAndNameAsync Error:  { venueResult?.Error }
                                                        apprenticeshipLocation.RecordStatus =
                                                            RecordStatus.MigrationPending;
                                                        adminReport +=
                                                            $"LocationName ( {location.LocationName} )Problem with the VenueService - GetVenuesByPRNAndNameAsync - Error:  {venuesResult?.Error}" +
                                                            Environment.NewLine;
                                                    }

                                                    locationBasedApprenticeshipLocation.Add(apprenticeshipLocation);

                                                    #endregion

                                                } // Region or SubRegion Locations
                                                else if (apprenticeshipLocation.ApprenticeshipLocationType.Equals(
                                                    ApprenticeshipLocationType.EmployerBased))
                                                {
                                                    #region Region or SubRegion Locations

                                                    var allRegionsWithSubRegions = new SelectRegionModel();

                                                    var onspdRegionSubregion = _onspdService.GetOnspdData(
                                                        new OnspdSearchCriteria(location.Postcode));

                                                    if (onspdRegionSubregion.IsFailure)
                                                    {
                                                        adminReport +=
                                                            $"* ATTENTION * {onspdRegionSubregion.Error}" +
                                                            Environment.NewLine;
                                                    }else if(!onspdRegionSubregion.HasValue)

                                                    {
                                                        adminReport +=
                                                            $"* ATTENTION * unable to find address details for location {location.Postcode}" +
                                                            Environment.NewLine;
                                                    }
                                                    else
                                                    {
                                                        // It's SubRegion, but if SubRegion does not much get Region
                                                        var selectedSubRegion = allRegionsWithSubRegions.RegionItems.SelectMany(sr =>
                                                            sr.SubRegion.Where(sb =>
                                                            sb.SubRegionName == onspdRegionSubregion.Value.Value.LocalAuthority    
                                                            || sb.SubRegionName == onspdRegionSubregion.Value.Value.County
                                                            ||    onspdRegionSubregion.Value.Value.LocalAuthority.Contains(sb.SubRegionName)
                                                            ))
                                                            .FirstOrDefault();

                                                        if (selectedSubRegion == null)
                                                        {
                                                                // Problem - No selectedRegion and NO selectedSubRegion match => Undefined 
                                                                adminReport +=
                                                                    $"* ATTENTION * After NOT be able to identify SubRegion, we couldn't identify a Region for " +
                                                                    $"( {onspdRegionSubregion.Value.Value.Region} ) and Postcode ( {location.Postcode} ). " +
                                                                    Environment.NewLine;
                                                                apprenticeshipLocation.RecordStatus =
                                                                    RecordStatus.MigrationPending;
                                                                continue;
                                                        }
                                                        else
                                                        {
                                                            apprenticeshipLocation.LocationId =
                                                                selectedSubRegion.ApiLocationId;
                                                            apprenticeshipLocation.LocationType =
                                                                LocationType.SubRegion;
                                                            apprenticeshipLocation.Radius =
                                                                _settings.SubRegionBasedRadius;
                                                            apprenticeshipLocation.Regions =
                                                                new[] { selectedSubRegion.Id };
                                                            adminReport +=
                                                                $" We've identified a SubRegion ( {onspdRegionSubregion.Value.Value.Region} ) with ID ( {selectedSubRegion.ApiLocationId} ) " +
                                                                Environment.NewLine;
                                                        }

                                                    }

                                                    regionBasedApprenticeshipLocation.Add(apprenticeshipLocation);

                                                    #endregion
                                                }
                                                else
                                                {
                                                    // Undefined - apprenticeshipLocation.ApprenticeshipLocationType
                                                    apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                }

                                            }
                                        }
                                    }

                                    adminReport += $"ApprenticeshipLocation Status ( { apprenticeshipLocation.RecordStatus } )." + Environment.NewLine;
                                }

                                if (regionBasedApprenticeshipLocation.Any(x =>
                                    x.RecordStatus == RecordStatus.Live))
                                {
                                    var regionLocation =
                                        regionBasedApprenticeshipLocation.FirstOrDefault(x =>
                                            x.RecordStatus == RecordStatus.Live);
                                    regionLocation.Regions = regionBasedApprenticeshipLocation
                                        .Where(x => x.Regions != null)
                                        .SelectMany(x => x.Regions).Distinct().ToArray();
                                    locationBasedApprenticeshipLocation.Add(
                                        regionLocation
                                        );

                                }

                                apprenticeship.ApprenticeshipLocations = locationBasedApprenticeshipLocation;

                                CountApprenticeshipLocationsPerAppr = apprenticeship.ApprenticeshipLocations.Count();
                                CountApprenticeshipLocationsPendingPerAppr = apprenticeship.ApprenticeshipLocations.Where(x => x.RecordStatus == RecordStatus.MigrationPending).Count();
                                CountApprenticeshipLocationsLivePerAppr = apprenticeship.ApprenticeshipLocations.Where(x => x.RecordStatus == RecordStatus.Live).Count();

                                adminReport += $"Apprenticeship Status ( { apprenticeship.RecordStatus } )." + Environment.NewLine;
                                adminReport += $"Apprenticeship has ( { CountApprenticeshipLocationsPerAppr } ) ApprenticeshipLocations - Pending ( { CountApprenticeshipLocationsPendingPerAppr } ) and Live ( { CountApprenticeshipLocationsLivePerAppr } )." + Environment.NewLine;

                                CountApprenticeshipLocations = CountApprenticeshipLocations + CountApprenticeshipLocationsPerAppr;
                                CountApprenticeshipLocationsPending = CountApprenticeshipLocationsPending + CountApprenticeshipLocationsPendingPerAppr;
                                CountApprenticeshipLocationsLive = CountApprenticeshipLocationsLive + CountApprenticeshipLocationsLivePerAppr;
                            }

                            if (isValidApprenticeship)
                            {

                                if (apprenticeship.RecordStatus.HasFlag(RecordStatus.MigrationPending))
                                {
                                    CountApprenticeshipPending++;
                                }
                                else
                                {
                                    CountApprenticeshipLive++;
                                }

                                var apprenticeshipResult =
                                    await _apprenticeshipService.AddApprenticeshipAsync(apprenticeship);
                                if (apprenticeshipResult.IsSuccess && apprenticeshipResult.HasValue)
                                {

                                    adminReport += Environment.NewLine + $"The apprenticeship has been migrated  " +
                                                   Environment.NewLine;
                                }
                                else
                                {
                                    CountAppreticeshipFailedToMigrate++;
                                    adminReport += Environment.NewLine +
                                                   $"The apprenticeship has not been migrated. Error -  {apprenticeshipResult.Error}  " +
                                                   Environment.NewLine;
                                }
                            }
                            else
                            {
                                // Create somekind of apprenticeship migration unknown report
                            }
                        }
                    }
                }

                providerStopWatch.Stop();

                //CountApprenticeships = appre
                adminReport += $"Number of Apprenticeships migrated ( { CountApprenticeships  } ) with Pending ( { CountApprenticeshipPending } ) and Live ( { CountApprenticeshipLive} ) Status" + Environment.NewLine;
                CountAllApprenticeships = CountAllApprenticeships + CountApprenticeships;
                CountAllApprenticeshipPending = CountAllApprenticeshipPending + CountApprenticeshipPending;
                CountAllApprenticeshipLive = CountAllApprenticeshipLive + CountApprenticeshipLive;
                CountAllUnknownStandardsOrFrameworks =
                    CountAllUnknownStandardsOrFrameworks + CountUnknownStandardsOrFrameworks;
                CountAllApprenticeshipLocations = CountAllApprenticeshipLocations + CountApprenticeshipLocations;
                CountAllApprenticeshipLocationsPending = CountAllApprenticeshipLocationsPending + CountApprenticeshipLocationsPending;
                CountAllApprenticeshipLocationsLive = CountAllApprenticeshipLocationsLive + CountApprenticeshipLocationsLive;

                provStopWatch.Stop();
                //string formatedStopWatchElapsedTime = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}:{4:D3}", stopWatch.Elapsed.Days, stopWatch.Elapsed.Hours, stopWatch.Elapsed.Minutes, stopWatch.Elapsed.Seconds, stopWatch.Elapsed.Milliseconds);
                Console.WriteLine("Time taken for Provider:" + provStopWatch.Elapsed.ToString());
                provStopWatch.Start();
            }

            adminStopWatch.Stop();
            adminReport += "_________________________________________________________________________________________________________" + Environment.NewLine;
            adminReport += $"Number of Providers migrated ( { CountProviders } ). Total time taken: { adminStopWatch.Elapsed }" + Environment.NewLine;
            adminReport += $"Number of Apprenticeships missing a Framework or standard ( { CountAllUnknownStandardsOrFrameworks } )." + Environment.NewLine;
            adminReport += $"Number of ALL Apprenticeships migrated ( { CountAllApprenticeships  } ) with Pending ( { CountAllApprenticeshipPending } ) and Live ( { CountAllApprenticeshipLive} ) Status" + Environment.NewLine;
            adminReport += $"Number of ALL ApprenticeshipLocations migrated ( { CountAllApprenticeshipLocations  } ) with Pending ( { CountAllApprenticeshipLocationsPending } ) and Live ( { CountAllApprenticeshipLocationsLive } ) Status" + Environment.NewLine;

            if (_settings.GenerateReportFilesLocally)
            {
                var adminReportFileName = string.Format("{0}-AdminReport-{1}.txt", DateTime.Now.ToString("yyMMdd-HHmmss"), CountProviders.ToString());
                string AdminReportsPath = string.Format(@"{0}\AdminReports", _settings.JsonApprenticeshipFilesPath);
                if (!Directory.Exists(AdminReportsPath))
                    Directory.CreateDirectory(AdminReportsPath);
                File.WriteAllText(string.Format(@"{0}\{1}", AdminReportsPath, adminReportFileName), adminReport);
            }
            Console.WriteLine("Migration of Apprenticeships completed.");
            string lastLine = Console.ReadLine();
        }

        internal static bool CheckForValidUKPRN(string ukprn)
        {
            string regex = "^[1][0-9]{7}$";
            var validUKPRN = Regex.Match(ukprn, regex, RegexOptions.IgnoreCase);

            return validUKPRN.Success;
        }

        private static async Task<IResult> UpdateProviderType(IProviderService providerService, Provider provider)
        {
            switch (provider.ProviderType)
            {
                case ProviderType.Both:
                case ProviderType.Apprenticeship:
                    return null;
                case ProviderType.FE:
                    provider.ProviderType = ProviderType.Both;
                    break;
                default:
                    provider.ProviderType = ProviderType.Apprenticeship;
                    break;
            }

            return await providerService.UpdateProviderDetails(provider);
        }
    }
}

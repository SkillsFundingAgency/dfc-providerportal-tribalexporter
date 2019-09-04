using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Interfaces.Providers;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Regions;
using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.CourseDirectory.Services.ApprenticeshipService;
using Dfc.CourseDirectory.Services.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService;
using Dfc.CourseDirectory.Services.Interfaces.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using Dfc.CourseDirectory.Services.ProviderService;
using Dfc.CourseDirectory.Services.VenueService;
using Dfc.ProviderPortal.ApprenticeshipMigration.Helpers;
using Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces;
using Dfc.ProviderPortal.ApprenticeshipMigration.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Providercontact = Dfc.ProviderPortal.ApprenticeshipMigration.Models.Providercontact;

namespace Dfc.ProviderPortal.ApprenticeshipMigration
{
    public class ApprenticeshipMigration : IApprenticeshipMigration
    {
        private readonly IConfigurationRoot configuration;

        private readonly IVenueService _venueService;
        private readonly IProviderService _providerService;
        private readonly IApprenticeReferenceDataService _apprenticeReferenceDataService;
        private readonly IApprenticeshipService _apprenticeshipService;
        private readonly IBlobStorageService _blobService;
        private readonly ILogger _logger;
        private readonly ApprenticeshipMigrationSettings settings;


        ApprenticeshipMigration(BlobStorageServiceResolver blobStorageServiceResolver, IOptions<ApprenticeshipMigrationSettings> settings)
        {
            this.settings = settings.Value;
            _blobService = blobStorageServiceResolver(nameof(ApprenticeshipMigration));
        }
        public async Task RunApprenticeShipMigration()
        {

            _logger.LogDebug("Starting Dfc.CourseDirectory.ApprenticeshipMigrationTool application");


            _logger.LogDebug("Log test.");




            #region Get User Input and Set Variables

            string adminReport = "                         Admin Report " + Environment.NewLine;
            adminReport += "________________________________________________________________________________" + Environment.NewLine + Environment.NewLine;

            var providerUKPRNList = new List<int>();
            int courseTransferId = 0;
            bool goodToTransfer = false;
            TransferMethod transferMethod = TransferMethod.Undefined;
            int? singleProviderUKPRN = null;
            string bulkUploadFileName = string.Empty;



            Console.WriteLine("The Migration Tool is running in Blob Mode." + Environment.NewLine + "Please, do not close this window until \"Migration completed\" message is displayed." + Environment.NewLine);

            string errorMessageGetCourses = string.Empty;
            providerUKPRNList = await  _blobService.GetBulkUploadProviderListFileAsync(2);
            if (!string.IsNullOrEmpty(errorMessageGetCourses))
            {
                adminReport += errorMessageGetCourses + Environment.NewLine;
            }
            else
            {
                goodToTransfer = true;
                transferMethod = TransferMethod.CourseMigrationTool;
            }




            // Auditing to be decided
            //if (goodToTransfer)
            //{
            //    string errorMessageCourseTransferAdd = string.Empty;
            //    if (providerUKPRNList != null && providerUKPRNList.Count > 0)
            //    {
            //        DataHelper.CourseTransferAdd(connectionString,
            //                                        DateTime.Now,
            //                                        (int)transferMethod,
            //                                        (int)deploymentEnvironment,
            //                                        string.Empty,
            //                                        "DFC – Course Migration Tool",
            //                                        singleProviderUKPRN,
            //                                        out errorMessageCourseTransferAdd,
            //                                        out courseTransferId);
            //    }
            //    if (!string.IsNullOrEmpty(errorMessageCourseTransferAdd)) adminReport += errorMessageCourseTransferAdd + Environment.NewLine;

            //    if (courseTransferId.Equals(-1))
            //    {
            //        adminReport += $"We cannot get the BatchNumber (CourseTransferId), so migration will be terminated. Number of UKPRNs ( { providerUKPRNList?.Count } )" + Environment.NewLine;
            //        providerUKPRNList = null;
            //    }
            //}


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

            #endregion

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

                string providerReport = "                         Migration Report " + Environment.NewLine;

                // GetProviderDetailsByUKPRN
                string errorMessageGetProviderDetailsByUKPRN = string.Empty;
                var provider = DataHelper.GetProviderDetailsByUKPRN(providerUKPRN, settings.ConnectionString, out errorMessageGetProviderDetailsByUKPRN);
                var ProviderGuidId = new Guid();
                var TribalProviderId = provider.ProviderId;
                string providerUkprnLine = "Provider - " + providerUKPRN + " - " + provider.ProviderName;
                Console.WriteLine(providerUkprnLine);
                adminReport += "_________________________________________________________________________________________________________" + Environment.NewLine;
                adminReport += Environment.NewLine + providerUkprnLine + Environment.NewLine;

                if (!string.IsNullOrEmpty(errorMessageGetProviderDetailsByUKPRN))
                {
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

                            if (settings.UpdateProvider)
                            {
                                // Commented out fields are not updated
                                //providerToUpdate.ProviderName = provider.ProviderName;
                                //providerToUpdate.TradingName = provider.TradingName;
                                providerToUpdate.ProviderId = provider.ProviderId;
                                providerToUpdate.UPIN = provider.UPIN;
                                providerToUpdate.MarketingInformation = provider.MarketingInformation;

                                //if (!string.IsNullOrEmpty(provider.ProviderNameAlias))
                                //{
                                //    if (providerToUpdate.ProviderAliases != null && providerToUpdate.ProviderAliases[0].ProviderAlias == null)
                                //    {
                                //        var providerAlias = new Provideralias();
                                //        providerAlias.ProviderAlias = provider.ProviderNameAlias;
                                //        providerAlias.LastUpdated = DateTime.Now;
                                //        providerToUpdate.ProviderAliases = new IProvideralias[] { providerAlias };
                                //    }
                                //}

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


                                if (settings.GenerateJsonFilesLocally)
                                {
                                    var providerToUpdateJson = JsonConvert.SerializeObject(providerToUpdate);
                                    string jsonUpdateProviderPathFileName = string.Format("{0}-UpdateProvider-{1}.json", DateTime.Now.ToString("yyMMdd-HHmmss"), providerToUpdate.UnitedKingdomProviderReferenceNumber);
                                    string UpdateProviderPath = string.Format(@"{0}\UpdateProvider", settings.JsonApprenticeshipFilesPath);
                                    if (!Directory.Exists(UpdateProviderPath))
                                        Directory.CreateDirectory(UpdateProviderPath);
                                    File.WriteAllText(string.Format(@"{0}\{1}", UpdateProviderPath, jsonUpdateProviderPathFileName), providerToUpdateJson);
                                }
                                else
                                {
                                    // Call ProviderService API to update provider
                                    var updateProviderResult = await _providerService.UpdateProviderDetails(providerToUpdate);
                                    if (updateProviderResult.IsSuccess)
                                    {
                                        adminReport += $"Provider ( { providerToUpdate.ProviderName } ) with UKPRN ( { providerToUpdate.UnitedKingdomProviderReferenceNumber } ) was updated in CosmosDB" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        adminReport += $"*** ATTENTION *** - Problem with the ProviderService - UpdateProviderDetails -  Error:  { updateProviderResult?.Error }" + Environment.NewLine;
                                    }
                                }
                            }

                            #endregion
                        }
                        else
                        {
                            providerReport += $"We CANNOT IDENTIFY the Provider - " + Environment.NewLine + Environment.NewLine;
                        }
                    }
                    else
                    {
                        providerReport += $"ERROR on GETTING the Provider - { providerResult.Error }" + Environment.NewLine + Environment.NewLine;
                    }

                    if (settings.DeleteCoursesByUKPRN)
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
                            Console.Write("ERROR: Failed to delete provider current courses for: " + providerUKPRN);
                            providerReport += $"Error on deleteing courses -  { deleteCoursesByUKPRNResult.Error }  " + Environment.NewLine;
                        }
                    }

                    // Get Apprenticeships by ProviderId                    
                    string errorMessageGetApprenticeshipsByProviderId = string.Empty;
                    var apprenticeships = DataHelper.GetApprenticeshipsByProviderId(provider.ProviderId ?? 0, settings.ConnectionString, out errorMessageGetApprenticeshipsByProviderId);
                    if (!string.IsNullOrEmpty(errorMessageGetApprenticeshipsByProviderId))
                    {
                        adminReport += $"* ATTENTION * { errorMessageGetApprenticeshipsByProviderId }" + Environment.NewLine;
                    }
                    else
                    {
                        CountApprenticeships = apprenticeships.Count;


                        foreach (var apprenticeship in apprenticeships)
                        {
                            int CountApprenticeshipLocationsPerAppr = 0;
                            int CountApprenticeshipLocationsPendingPerAppr = 0;
                            int CountApprenticeshipLocationsLivePerAppr = 0;

                            // // Mapp Apprenticeships
                            apprenticeship.id = Guid.NewGuid();
                            apprenticeship.ProviderId = ProviderGuidId;
                            apprenticeship.ProviderUKPRN = providerUKPRN;
                            apprenticeship.TribalProviderId = TribalProviderId;

                            apprenticeship.RecordStatus = RecordStatus.Live;

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
                                    apprenticeship.RecordStatus = RecordStatus.MigrationPending;
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
                                    adminReport += $"> Standard Apprenticeship - StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;

                                }
                                else
                                {
                                    apprenticeship.ApprenticeshipType = ApprenticeshipType.Undefined;
                                    apprenticeship.RecordStatus = RecordStatus.MigrationPending;
                                    adminReport += $"> * ATTENTION * Apprenticeship NOT Defined - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } ), StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;
                                }
                            }
                            else
                            {
                                apprenticeship.ApprenticeshipType = ApprenticeshipType.Undefined;
                                apprenticeship.RecordStatus = RecordStatus.MigrationPending;
                                adminReport += $"> * ATTENTION * Apprenticeship NOT Defined - FrameworkCode ( { apprenticeship.FrameworkCode } ), ProgType ( { apprenticeship.ProgType } ), PathwayCode ( { apprenticeship.PathwayCode } ), StandardCode ( { apprenticeship.StandardCode } ), Version ( { apprenticeship.Version } )" + Environment.NewLine;
                            }


                            // Get ApprenticeshipLocations                          
                            string errorMessageGetApprenticeshipLocations = string.Empty;
                            var apprenticeshipLocations = DataHelper.GetApprenticeshipLocationsByApprenticeshipId(apprenticeship.ApprenticeshipId ?? 0, settings.ConnectionString, out errorMessageGetApprenticeshipLocations);
                            if (!string.IsNullOrEmpty(errorMessageGetApprenticeshipLocations))
                            {
                                adminReport += $"* ATTENTION * { errorMessageGetApprenticeshipLocations }" + Environment.NewLine;
                            }
                            else
                            {
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
                                    var deliveryModes = DataHelper.GetDeliveryModesByApprenticeshipLocationId(apprenticeshipLocation.ApprenticeshipLocationId, settings.ConnectionString, out errorMessageGetDeliveryModes);
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
                                            apprenticeshipLocation.Radius = settings.VenueBasedRadius; // Leave it as it is. COUR-419
                                            adminReport += $" - ApprenticeshipLocationType ( { apprenticeshipLocation.ApprenticeshipLocationType } )" + Environment.NewLine;
                                        }
                                        else if ((!deliveryModes.Contains(1) && !deliveryModes.Contains(2) && deliveryModes.Contains(3)) ||
                                                 (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && !deliveryModes.Contains(3)) ||
                                                 (!deliveryModes.Contains(1) && deliveryModes.Contains(2) && deliveryModes.Contains(3)))
                                        {
                                            apprenticeshipLocation.ApprenticeshipLocationType = ApprenticeshipLocationType.ClassroomBased;
                                            apprenticeshipLocation.LocationType = LocationType.Venue;
                                            apprenticeshipLocation.Radius = settings.VenueBasedRadius;
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
                                        var location = DataHelper.GetLocationByLocationIdPerProvider(apprenticeshipLocation.LocationId ?? 0, provider.ProviderId ?? 0, settings.ConnectionString, out errorMessageGetTribalLocation);
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
                                                adminReport += $"We couldn't get location for LocationId ({ apprenticeshipLocation.LocationId }) " + Environment.NewLine;
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
                                            }
                                        }


                                        // Venue Locations
                                        if ((apprenticeshipLocation.ApprenticeshipLocationType.Equals(ApprenticeshipLocationType.ClassroomBased)) ||
                                        apprenticeshipLocation.ApprenticeshipLocationType.Equals(ApprenticeshipLocationType.ClassroomBasedAndEmployerBased))
                                        {
                                            #region Venue Locations

                                            GetVenuesByPRNAndNameCriteria venueCriteria = new GetVenuesByPRNAndNameCriteria(apprenticeship.ProviderUKPRN.ToString(), location.LocationName);
                                            var venuesResult = await _venueService.GetVenuesByPRNAndNameAsync(venueCriteria);

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
                                                        apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                        adminReport += $"*** ATTENTION *** We don't have valid LocationId assigned by our VenueService API - Location/VenueName ({ location.LocationName }). " + Environment.NewLine;
                                                    }
                                                    else
                                                    {
                                                        apprenticeshipLocation.LocationId = venue.LocationId;
                                                        apprenticeshipLocation.LocationGuidId = new Guid(venue.ID);
                                                        apprenticeshipLocation.Address = new Address()
                                                        {
                                                            Address1 = venue.Address1,
                                                            Address2 = venue.Address2,
                                                            County = venue.County,
                                                            Email = venue.Email,
                                                            Latitude = double.Parse(venue.Latitude.ToString(CultureInfo.InvariantCulture)),
                                                            Longitude = double.Parse(venue.Longitude.ToString(CultureInfo.InvariantCulture)),
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
                                                                adminReport += $"It's a good match " + Environment.NewLine;
                                                            }
                                                            else
                                                            {
                                                                // If it's different we will check whether Venue is TribalLocation. 
                                                                if (venue.TribalLocationId != null || venue.TribalLocationId.Equals(0))
                                                                {
                                                                    // Venue is also TribalLocation. But it was used previosly. Therefor we do NOT update anything. Just use it. (These overides AL - TribalLocation relation.)  
                                                                    adminReport += $"Venue is also TribalLocation. But it was used previosly. Therefor we do NOT update anything. Just use it." + Environment.NewLine;
                                                                }
                                                                else
                                                                {
                                                                    // Venue is NOT TribalLocation. We can use it but we have to update Contacts.           
                                                                    adminReport += $"Venue is NOT TribalLocation. We can use it but we have to update Contacts." + Environment.NewLine;
                                                                    UpdateVenue = true;
                                                                    venue.PHONE = location.Telephone;
                                                                    venue.EMAIL = location.Email;
                                                                    venue.WEBSITE = location.Website;
                                                                    venue.TribalLocationId = location.LocationId;
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // TODO _ CHECK IT OUT
                                                            // Venue has Status ( {venue.Status} ). We will bring it to LIVE and update Contacts.
                                                            adminReport += $"Venue has Status ( {venue.Status} ). We will bring it to LIVE and update Contacts." + Environment.NewLine;

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
                                                        if (settings.GenerateJsonFilesLocally)
                                                        {
                                                            var updateVenueJson = JsonConvert.SerializeObject(venue);
                                                            string jsonFileName = string.Format("{0}-UpdateVenue-{1}-{2}.json", DateTime.Now.ToString("yyMMdd-HHmmss"), providerUKPRN, location.LocationName.Replace(" ", string.Empty).Replace(@"\", string.Empty).Replace("/", string.Empty));
                                                            string UpdateVenuePath = string.Format(@"{0}\UpdateVenue", settings.JsonApprenticeshipFilesPath);
                                                            if (!Directory.Exists(UpdateVenuePath))
                                                                Directory.CreateDirectory(UpdateVenuePath);
                                                            File.WriteAllText(string.Format(@"{0}\{1}", UpdateVenuePath, jsonFileName), updateVenueJson);
                                                        }
                                                        else
                                                        {
                                                            var updateVenueResult = await _venueService.UpdateAsync(venue);
                                                            if (updateVenueResult.IsSuccess && updateVenueResult.HasValue)
                                                            {
                                                                adminReport += $"Venue ( { venue.VenueName } ) was updated in CosmosDB" + Environment.NewLine;
                                                            }
                                                            else
                                                            {
                                                                // Problem with the service  - UpdateAsync -  Error:  { updateVenueResult?.Error }
                                                                apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                                adminReport += $"Problem with the service - UpdateAsync -  Error:  { updateVenueResult?.Error }" + Environment.NewLine;
                                                            }
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
                                                        Latitude = double.Parse(location.Latitude.ToString(CultureInfo.InvariantCulture)),
                                                        Longitude = double.Parse(location.Longitude.ToString(CultureInfo.InvariantCulture)),
                                                        Phone = location.Telephone,
                                                        Postcode = location.Postcode,
                                                        Town = location.Town,
                                                        Website = location.Website
                                                    };

                                                    adminReport += $"Adds Venue for LocationName ({ location.LocationName })" + Environment.NewLine;

                                                    if (settings.GenerateJsonFilesLocally)
                                                    {
                                                        var addVenueJson = JsonConvert.SerializeObject(addVenue);
                                                        string jsonFileName = string.Format("{0}-Venue-{1}-{2}.json", DateTime.Now.ToString("yyMMdd-HHmmss"), providerUKPRN, location.LocationName.Replace(" ", string.Empty).Replace(@"\", string.Empty).Replace("/", string.Empty));
                                                        string AddVenuePath = string.Format(@"{0}\AddVenue", settings.JsonApprenticeshipFilesPath);
                                                        if (!Directory.Exists(AddVenuePath))
                                                            Directory.CreateDirectory(AddVenuePath);
                                                        File.WriteAllText(string.Format(@"{0}\{1}", AddVenuePath, jsonFileName), addVenueJson);
                                                    }
                                                    else
                                                    {
                                                        var addVenueResult = await _venueService.AddAsync(addVenue);
                                                        if (addVenueResult.IsSuccess && addVenueResult.HasValue)
                                                        {
                                                            // All good => It's important to update apprenticeshipLocation.LocationId
                                                            apprenticeshipLocation.LocationId = ((Venue)addVenueResult.Value).LocationId;
                                                            adminReport += $"All good => It's important to update apprenticeshipLocation.LocationId" + Environment.NewLine;
                                                        }
                                                        else
                                                        {
                                                            // Problem with the service Error:  { addVenueResult?.Error }
                                                            apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                            adminReport += $"Problem with the service - AddAsync -  Error:  { addVenueResult?.Error }" + Environment.NewLine;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // We have multiple Venues for the same name ({ location.LocationName }).  Issue raised many times
                                                    adminReport += $"Multiple Venues for the same name ({ location.LocationName }). " + Environment.NewLine;
                                                    apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                }
                                            }
                                            else
                                            {

                                                // Problem with the service GetVenuesByPRNAndNameAsync Error:  { venueResult?.Error }
                                                apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                adminReport += $"LocationName ( { location.LocationName } )Problem with the VenueService - GetVenuesByPRNAndNameAsync - Error:  { venuesResult?.Error }" + Environment.NewLine;
                                            }

                                            #endregion

                                        } // Region or SubRegion Locations
                                        else if (apprenticeshipLocation.ApprenticeshipLocationType.Equals(ApprenticeshipLocationType.EmployerBased))
                                        {
                                            #region Region or SubRegion Locations
                                            var allRegionsWithSubRegions = new SelectRegionModel();

                                            string errorMessageGetRegionSubRegion = string.Empty;
                                            var onspdRegionSubregion = DataHelper.GetRegionSubRegionByPostcode(location.Postcode, settings.ConnectionString, out errorMessageGetRegionSubRegion);
                                            if (!string.IsNullOrEmpty(errorMessageGetRegionSubRegion))
                                            {
                                                adminReport += $"* ATTENTION * { errorMessageGetRegionSubRegion }" + Environment.NewLine;
                                            }
                                            else
                                            {
                                                if (apprenticeshipLocation.Radius > settings.RegionSubRegionRangeRadius)
                                                {
                                                    // It's Region
                                                    var selectedRegion = allRegionsWithSubRegions.RegionItems.Where(x => x.RegionName == onspdRegionSubregion.Region).SingleOrDefault();
                                                    if (selectedRegion == null)
                                                    {
                                                        // Problem - No selectedRegion match => Undefined 
                                                        adminReport += $"* ATTENTION * We couldn't identify a Region for ( { onspdRegionSubregion.Region } ) and Postcode ( { location.Postcode } ). " + Environment.NewLine;
                                                        apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                    }
                                                    else
                                                    {
                                                        apprenticeshipLocation.LocationId = selectedRegion.ApiLocationId;
                                                        apprenticeshipLocation.LocationType = LocationType.Region;
                                                        apprenticeshipLocation.Radius = settings.RegionBasedRadius;
                                                        apprenticeshipLocation.Regions = selectedRegion.SubRegion.Select(x => x.Id).ToArray();
                                                        adminReport += $" We've identified a Region ( { onspdRegionSubregion.Region } ) with ID ( { selectedRegion.ApiLocationId } ) " + Environment.NewLine;
                                                    }
                                                }
                                                else
                                                {
                                                    // It's SubRegion, but if SubRegion does not much get Region
                                                    var selectedSubRegion = allRegionsWithSubRegions.RegionItems.Where(x => x.SubRegion.Any(sr => sr.SubRegionName == onspdRegionSubregion.SubRegion)).SingleOrDefault();
                                                    if (selectedSubRegion == null)
                                                    {
                                                        // Do Region
                                                        var selectedRegion = allRegionsWithSubRegions.RegionItems.Where(x => x.RegionName == onspdRegionSubregion.Region).SingleOrDefault();
                                                        if (selectedRegion == null)
                                                        {
                                                            // Problem - No selectedRegion and NO selectedSubRegion match => Undefined 
                                                            adminReport += $"* ATTENTION * After NOT be able to identify SubRegion, we couldn't identify a Region for ( { onspdRegionSubregion.Region } ) and Postcode ( { location.Postcode } ). " + Environment.NewLine;
                                                            apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                                        }
                                                        else
                                                        {
                                                            apprenticeshipLocation.LocationId = selectedRegion.ApiLocationId;
                                                            apprenticeshipLocation.LocationType = LocationType.Region;
                                                            apprenticeshipLocation.Radius = settings.RegionBasedRadius;
                                                            adminReport += $"After NOT be able to identify SubRegion, we've identified a Region ( { onspdRegionSubregion.Region } ) with ID ( { selectedRegion.ApiLocationId } ) " + Environment.NewLine;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        apprenticeshipLocation.LocationId = selectedSubRegion.SubRegion.Where(x => x.SubRegionName == onspdRegionSubregion.SubRegion).SingleOrDefault().ApiLocationId;
                                                        apprenticeshipLocation.LocationType = LocationType.SubRegion;
                                                        apprenticeshipLocation.Radius = settings.SubRegionBasedRadius;
                                                        adminReport += $" We've identified a SubRegion ( { onspdRegionSubregion.SubRegion } ) with ID ( { selectedSubRegion.ApiLocationId } ) " + Environment.NewLine;
                                                    }
                                                }
                                            }

                                            #endregion
                                        }
                                        else
                                        {
                                            // Undefined - apprenticeshipLocation.ApprenticeshipLocationType
                                            apprenticeshipLocation.RecordStatus = RecordStatus.MigrationPending;
                                        }
                                    }

                                    adminReport += $"ApprenticeshipLocation Status ( { apprenticeshipLocation.RecordStatus } )." + Environment.NewLine;
                                }


                                apprenticeship.ApprenticeshipLocations = apprenticeshipLocations;

                                CountApprenticeshipLocationsPerAppr = apprenticeship.ApprenticeshipLocations.Count();
                                CountApprenticeshipLocationsPendingPerAppr = apprenticeship.ApprenticeshipLocations.Where(x => x.RecordStatus == RecordStatus.MigrationPending).Count();
                                CountApprenticeshipLocationsLivePerAppr = apprenticeship.ApprenticeshipLocations.Where(x => x.RecordStatus == RecordStatus.Live).Count();

                                adminReport += $"Apprenticeship Status ( { apprenticeship.RecordStatus } )." + Environment.NewLine;
                                adminReport += $"Apprenticeship has ( { CountApprenticeshipLocationsPerAppr } ) ApprenticeshipLocations - Pending ( { CountApprenticeshipLocationsPendingPerAppr } ) and Live ( { CountApprenticeshipLocationsLivePerAppr } )." + Environment.NewLine;

                                CountApprenticeshipLocations = CountApprenticeshipLocations + CountApprenticeshipLocationsPerAppr;
                                CountApprenticeshipLocationsPending = CountApprenticeshipLocationsPending + CountApprenticeshipLocationsPendingPerAppr;
                                CountApprenticeshipLocationsLive = CountApprenticeshipLocationsLive + CountApprenticeshipLocationsLivePerAppr;
                            }


                            if (apprenticeship.RecordStatus.Equals(RecordStatus.MigrationPending)) CountApprenticeshipPending++;
                            if (apprenticeship.RecordStatus.Equals(RecordStatus.Live)) CountApprenticeshipLive++;

                            // Add Apprenticeship to CosmosDB
                            if (settings.GenerateJsonFilesLocally)
                            {
                                var apprenticeshipJson = JsonConvert.SerializeObject(apprenticeship);
                                string jsonFileName = string.Format("{0}-Apprenticeship-{1}-{2}-{3}.json", DateTime.Now.ToString("yyMMdd-HHmmss"), providerUKPRN, apprenticeship.ApprenticeshipId, apprenticeship?.ApprenticeshipLocations?.Count());
                                if (!Directory.Exists(settings.JsonApprenticeshipFilesPath))
                                    Directory.CreateDirectory(settings.JsonApprenticeshipFilesPath);
                                File.WriteAllText(string.Format(@"{0}\{1}", settings.JsonApprenticeshipFilesPath, jsonFileName), apprenticeshipJson);
                            }
                            else
                            {
                                var apprenticeshipResult = await _apprenticeshipService.AddApprenticeshipAsync(apprenticeship);
                                if (apprenticeshipResult.IsSuccess && apprenticeshipResult.HasValue)
                                {

                                    adminReport += Environment.NewLine + $"The course is migrated  " + Environment.NewLine;
                                }
                                else
                                {
                                    CountAppreticeshipFailedToMigrate++;
                                    adminReport += Environment.NewLine + $"The course is NOT migrated. Error -  { apprenticeshipResult.Error }  " + Environment.NewLine;
                                }
                            }
                        }
                    }
                }

                providerStopWatch.Stop();
                //providerReport += $">>> Report { reportForProvider } - { providerReportFileName } - Time taken: { providerStopWatch.Elapsed } " + Environment.NewLine;
                // Write Provider Report

                //CountApprenticeships = appre
                adminReport += $"Number of Apprenticeships migrated ( { CountApprenticeships  } ) with Pending ( { CountApprenticeshipPending } ) and Live ( { CountApprenticeshipLive} ) Status" + Environment.NewLine;
                CountAllApprenticeships = CountAllApprenticeships + CountApprenticeships;
                CountAllApprenticeshipPending = CountAllApprenticeshipPending + CountApprenticeshipPending;
                CountAllApprenticeshipLive = CountAllApprenticeshipLive + CountApprenticeshipLive;

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
            adminReport += $"Number of ALL Apprenticeships migrated ( { CountAllApprenticeships  } ) with Pending ( { CountAllApprenticeshipPending } ) and Live ( { CountAllApprenticeshipLive} ) Status" + Environment.NewLine;
            adminReport += $"Number of ALL ApprenticeshipLocations migrated ( { CountAllApprenticeshipLocations  } ) with Pending ( { CountAllApprenticeshipLocationsPending } ) and Live ( { CountAllApprenticeshipLocationsLive } ) Status" + Environment.NewLine;

            if (settings.GenerateReportFilesLocally)
            {
                var adminReportFileName = string.Format("{0}-AdminReport-{1}.txt", DateTime.Now.ToString("yyMMdd-HHmmss"), CountProviders.ToString());
                string AdminReportsPath = string.Format(@"{0}\AdminReports", settings.JsonApprenticeshipFilesPath);
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
    }
}

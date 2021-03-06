﻿using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Dfc;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class VenueRestorer
    {
        [FunctionName(nameof(VenueRestorer))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger logger,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IVenueCollectionService venueCollectionService,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper
                    )
        {
            logger.LogDebug("VenueRestorer: Starting...");
            var connectionString = configuration.GetConnectionString("TribalRestore");
            var venuesCollectionId = configuration["CosmosDbCollectionSettings:VenuesCollectionId"];
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var container = configuration["BlobStorageSettings:Container"];
            var whiteListProviders = await GetProviderWhiteList();
            logger.LogDebug($"VenueRestorer: {whiteListProviders.Count} white-listed providers to process");
            var coursesCollectionId = "courses";
            var apprenticeshipCollectionId = "apprenticeship";
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var documentClient = cosmosDbHelper.GetClient();
            var venueCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);
            var venueCollection_OldUri = UriFactory.CreateDocumentCollectionUri(databaseId, "venues_old");
            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);
            var apprenticeshipCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var logFileName = $"VenueRestorer--{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var allVenues = GetAllOldVenues();
            var result = new List<ResultMessage>();
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";
            var ukprnCache = new List<int>();
            var updatedBy = "VenueRestorer";

            //grand totals
            var invalidCourseRunReferences = 0;
            var totalInvalidCourseRunReferences = 0;
            var uniqueInvalidVenues = new HashSet<string>();
            var replacedInvalidVenues = new HashSet<Venue>();
            var references = new List<VenueRestorerReference>();
            var rereferencedApprenticeshipLocations = 0;

            //provider scoped totals
            var totalInvalidApprenticeshipLocationReferences = 0;
            var invalidApprenticeshipLocationReferences = 0;

            int processedProviderCount = 0;
            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {
                foreach (var ukprn in whiteListProviders)
                {
                    //reference for old venue so that courseruns & apprenticeship locations can be
                    //re-referenced
                    var venuesReplacedForProvider = new List<Tuple<Venue, Venue, Guid>>();
                    try
                    {
                        //reset counters
                        invalidCourseRunReferences = 0;
                        invalidApprenticeshipLocationReferences = 0;

                        //fetch data for ukprn
                        var allCoursesForProvider = await GetCourses(ukprn);
                        var allApprenticeshipsForProvider = await GetApprenticeships(ukprn);
                        var venues = await GetVenues(ukprn);
                        var old_venues = GetOldVenues(ukprn);

                        //courses
                        foreach (var course in allCoursesForProvider)
                        {
                            //course runs
                            foreach (var courserun in course.CourseRuns)
                            {
                                //only courses that references a venue (classroom based or both)
                                if (courserun.VenueId != null && courserun.VenueId != Guid.Empty)
                                {
                                    //current venue & old venue (pre migration)
                                    var invalidReferencedVenue = await GetVenueById(courserun.VenueId?.ToString());
                                    var restoredVenue = old_venues.FirstOrDefault(x => new Guid(x.ID) == courserun.VenueId);

                                    //if current venues provider is different to course then attempt to replace it with
                                    //old venue from cosmos backup.
                                    if (invalidReferencedVenue != null && invalidReferencedVenue.UKPRN != course.ProviderUKPRN)
                                    {
                                        //replace existing venue with old venue if a match is found
                                        if (restoredVenue != null && restoredVenue.UKPRN == course.ProviderUKPRN)
                                        {
                                            await ReplaceVenue(invalidReferencedVenue.ID, restoredVenue, updatedBy);

                                            //the venue that was referenced needs to be inserted again but with a new id.
                                            var newId = Guid.NewGuid();
                                            await ReplaceVenue(newId.ToString(), invalidReferencedVenue, updatedBy);

                                            //reload venues as we have just replaced a venue
                                            venues = await GetVenues(ukprn);

                                            replacedInvalidVenues.Add(invalidReferencedVenue);

                                            //store old venue so that apprenticeship locations can be re-referenced
                                            venuesReplacedForProvider.Add(Tuple.Create(restoredVenue, invalidReferencedVenue, newId));

                                            //log changes
                                            references.Add(new VenueRestorerReference()
                                            {
                                                UKPRN = course.ProviderUKPRN,
                                                VenueId = courserun.VenueId.ToString(),
                                                CurrentVenueUKPRN = invalidReferencedVenue.UKPRN,
                                                CurrentAddress1 = invalidReferencedVenue.Address1,
                                                CurrentPostcode = invalidReferencedVenue.PostCode,
                                                CurrentVenueName = invalidReferencedVenue.VenueName,
                                                RestoredAddress1 = restoredVenue.Address1,
                                                RestoredVenueName = restoredVenue.VenueName,
                                                RestoredPostcode = restoredVenue.PostCode,
                                                RestoredVenueUKPRN = restoredVenue.UKPRN,
                                                UKPRNMatched = (course.ProviderUKPRN == invalidReferencedVenue.UKPRN),
                                                Message = "Replaced Venue",
                                                Type = "Course",
                                                CourseId = course.id,
                                                CourseRunId = courserun.id,
                                            });
                                        }
                                        else
                                        {
                                            references.Add(new VenueRestorerReference()
                                            {
                                                UKPRN = course.ProviderUKPRN,
                                                VenueId = courserun.VenueId.ToString(),
                                                CurrentVenueUKPRN = invalidReferencedVenue.UKPRN,
                                                CurrentAddress1 = invalidReferencedVenue.Address1,
                                                CurrentPostcode = invalidReferencedVenue.PostCode,
                                                CurrentVenueName = invalidReferencedVenue.VenueName,
                                                UKPRNMatched = (course.ProviderUKPRN == invalidReferencedVenue.UKPRN),
                                                Message = "Unable to replace Venue, as old venue was not found in backup",
                                                Type = "Course",
                                                CourseId = course.id,
                                                CourseRunId = courserun.id,
                                            });
                                        }

                                        //invalid references
                                        uniqueInvalidVenues.Add(invalidReferencedVenue.ID);
                                        invalidCourseRunReferences++;
                                    }
                                }
                            }

                            //total for all providers
                            totalInvalidCourseRunReferences += invalidCourseRunReferences;
                        }

                        //apprenticeships
                        foreach (var apprenticeship in allApprenticeshipsForProvider)
                        {
                            //apprenticeship locations
                            foreach (var location in apprenticeship.ApprenticeshipLocations)
                            {
                                //only apprenticeshiplocations that references a venue (classroom based or both)
                                if (location.VenueId.HasValue && location.LocationGuidId.HasValue && location.LocationGuidId != Guid.Empty)
                                {
                                    var invalidReferencedVenue = await GetVenueById(location.LocationGuidId?.ToString());

                                    if (invalidReferencedVenue != null && invalidReferencedVenue.UKPRN != apprenticeship.ProviderUKPRN)
                                    {
                                        var restoredVenue = old_venues.FirstOrDefault(x => new Guid(x.ID) == location.LocationGuidId);

                                        //replace existing venue with old venue if a match is found
                                        if (restoredVenue != null && restoredVenue.UKPRN == apprenticeship.ProviderUKPRN)
                                        {
                                            logger.LogDebug($"VenueRestorer: Invalid Apprenticeship location, apprenticeship should reference {restoredVenue.VenueName}");

                                            //old venue from json backup is the correct venue that should be referenced
                                            //swap invalid venue, with restoredVenue
                                            await ReplaceVenue(invalidReferencedVenue.ID, restoredVenue, updatedBy);

                                            //the venue that was referenced needs to be inserted again but with a new id.
                                            var newId = Guid.NewGuid();
                                            await ReplaceVenue(newId.ToString(), invalidReferencedVenue, updatedBy);

                                            //store old venue so that apprenticeship locations can be re-referenced
                                            venuesReplacedForProvider.Add(Tuple.Create(restoredVenue, invalidReferencedVenue, newId));

                                            //reload venues as we have just replaced a venue
                                            venues = await GetVenues(ukprn);

                                            //keep a track of the incorrect venue, these will be inserted with a new id.
                                            replacedInvalidVenues.Add(invalidReferencedVenue);

                                            references.Add(new VenueRestorerReference()
                                            {
                                                UKPRN = apprenticeship.ProviderUKPRN,
                                                ApprenticeshipLocationUKPRN = location.ProviderUKPRN,
                                                VenueId = location.LocationGuidId.ToString(),
                                                CurrentVenueUKPRN = invalidReferencedVenue.UKPRN,
                                                CurrentAddress1 = invalidReferencedVenue.Address1,
                                                CurrentPostcode = invalidReferencedVenue.PostCode,
                                                CurrentVenueName = invalidReferencedVenue.VenueName,
                                                RestoredVenueUKPRN = restoredVenue.UKPRN,
                                                RestoredAddress1 = restoredVenue?.Address1,
                                                RestoredPostcode = restoredVenue?.PostCode,
                                                RestoredVenueName = restoredVenue.VenueName,
                                                UKPRNMatched = (apprenticeship.ProviderUKPRN == invalidReferencedVenue.UKPRN),
                                                Message = "Replaced Venue",
                                                Type = "Apprenticeship",
                                                ApprenticeshipId = apprenticeship.id
                                            });
                                        }
                                        else
                                        {
                                            references.Add(new VenueRestorerReference()
                                            {
                                                UKPRN = apprenticeship.ProviderUKPRN,
                                                ApprenticeshipLocationUKPRN = location.ProviderUKPRN,
                                                UKPRNMatched = false,
                                                CurrentVenueUKPRN = -1,
                                                VenueId = location.LocationGuidId.ToString(),
                                                Type = "Apprenticeship",
                                                Message = "Unable to replace Venue, as old venue was not found in backup",
                                                ApprenticeshipId = apprenticeship.id
                                            });
                                        }
                                    }
                                }
                            }
                            totalInvalidApprenticeshipLocationReferences += invalidApprenticeshipLocationReferences;
                        }

                        //rereference apprenticeship locations
                        //if there is a venue that has been replaced but is referenced by an apprenticeshiplocation then the apprenticeship
                        //record needs to be updated to point the the new venue record to save data loss.
                        foreach (var apprenticeship in allApprenticeshipsForProvider)
                        {
                            var updated = false;
                            foreach(var location in apprenticeship.ApprenticeshipLocations)
                            {
                                var replacedVenue = venuesReplacedForProvider.FirstOrDefault(x => new Guid(x.Item2.ID) == location.LocationGuidId);
                                if(replacedVenue != null)
                                {
                                    updated = true;
                                    location.LocationGuidId = replacedVenue.Item3;
                                }
                            }

                            if (updated)
                            {
                                var documentLink = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, apprenticeship.id.ToString());
                                await documentClient.ReplaceDocumentAsync(documentLink, apprenticeship, new RequestOptions()
                                {
                                    PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                                });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"VenueRestorer: error for ukprn {ukprn}: {e.Message}", e);
                    }

                    processedProviderCount++;
                    if (processedProviderCount % 100 == 0)
                    {
                        logger.LogInformation($"VenueRestorer: {processedProviderCount}/{whiteListProviders.Count} providers processed.");
                    }
                    logger.LogDebug(
                        $"VenueRestorer: completed for UKPRN {ukprn}, {processedProviderCount}/{whiteListProviders.Count}. " +
                        $"{invalidCourseRunReferences} invalid venue references, " +
                        $"{invalidApprenticeshipLocationReferences} invalid apprenticeship references, " +
                        $"{rereferencedApprenticeshipLocations} apprenticeship locations were re-referenced.");
                }

                //write csv file
                logCsvWriter.WriteHeader(typeof(VenueRestorerReference));
                logCsvWriter.NextRecord();
                foreach (var reference in references)
                {
                    logCsvWriter.WriteRecord(reference);
                    logCsvWriter.NextRecord();
                }

                // Upload log CSV to blob storage
                logStreamWriter.Flush();
                logStream.Seek(0L, SeekOrigin.Begin);
                var blob = blobhelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                await blob.UploadFromStreamAsync(logStream);
                logger.LogInformation($"VenueRestorer: log uploaded as {logFileName}");
            }

            logger.LogInformation(
                $"VenueRestorer: completed. " +
                $"{totalInvalidCourseRunReferences} invalid CourseRun references in total, " +
                $"{totalInvalidApprenticeshipLocationReferences} Apprenticeship location invalid references in total, " +
                $"{replacedInvalidVenues.Count()} Venues have been reverted back to old venues, " +
                $"{uniqueInvalidVenues.Count()} Venues were invalid");

            async Task<List<Course>> GetCourses(int ukprn)
            {
                var courses = new List<Course>();
                //Get all courses
                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    };

                    var queryResponse = await documentClient.CreateDocumentQuery<Course>(coursesCollectionUri, feedOptions)
                        .Where(p => p.ProviderUKPRN == ukprn && p.CourseStatus != CourseDirectory.Models.Enums.RecordStatus.Archived)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Course>();

                    courses.AddRange(queryResponse.ToList());
                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);
                return courses;
            }

            async Task<List<Venue>> GetVenues(int ukprn)
            {
                var venues = new List<Venue>();
                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation
                    };

                    var queryResponse = await documentClient.CreateDocumentQuery<Venue>(venueCollectionUri, feedOptions)
                        .Where(p => p.UKPRN == ukprn)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Venue>();

                    venues.AddRange(queryResponse.ToList());

                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);
                return venues;
            }

            List<Venue> GetOldVenues(int ukprn)
            {
                return allVenues.Where(x => x.UKPRN == ukprn).ToList();
            }

            async Task<Venue> GetVenueById(string id)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);

                var query = documentClient
                    .CreateDocumentQuery<Venue>(collectionLink, new FeedOptions()
                    {
                        EnableCrossPartitionQuery = true
                    })
                    .Where(d => d.ID == id)
                    .AsDocumentQuery();

                return (await query.ExecuteNextAsync()).FirstOrDefault();
            }

            async Task<List<Apprenticeship>> GetApprenticeships(int ukprn)
            {
                var apprenticeships = new List<Apprenticeship>();
                string continuation = null;
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    };

                    try
                    {
                        var queryResponse = await documentClient.CreateDocumentQuery<Apprenticeship>(apprenticeshipCollectionUri, feedOptions)
                            .Where(p => p.ProviderUKPRN == ukprn && p.RecordStatus != CourseDirectory.Models.Enums.RecordStatus.Archived)
                            .AsDocumentQuery()
                            .ExecuteNextAsync<Apprenticeship>();

                        apprenticeships.AddRange(queryResponse);
                        continuation = queryResponse.ResponseContinuation;
                    }
                    catch (Exception)
                    {
                        continuation = null;
                    }
                }
                while (continuation != null);

                return apprenticeships;
            }

            async Task<ISet<int>> GetProviderWhiteList()
            {
                var blob = blobhelper.GetBlobContainer(blobContainer).GetBlockBlobReference(WHITE_LIST_FILE);
                var ms = new MemoryStream();
                await blob.DownloadToStreamAsync(ms);
                ms.Seek(0L, SeekOrigin.Begin);

                var results = new HashSet<int>();
                string line;
                using (var reader = new StreamReader(ms))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        var ukprn = int.Parse(line);
                        results.Add(ukprn);
                    }
                }
                return results;
            }

            async Task ReplaceVenue(string id, Venue matchedVenue, string updatedby)
            {
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);
                var editedVenue = new Dfc.CourseDirectory.Models.Models.Venues.Venue()
                {
                    ID = id,
                    UKPRN = matchedVenue.UKPRN,
                    VenueName = matchedVenue.VenueName,
                    Address1 = matchedVenue.Address1,
                    Address2 = matchedVenue.Address2,
                    Town = matchedVenue.Town,
                    County = matchedVenue.County,
                    PostCode = matchedVenue.PostCode,
                    Latitude = matchedVenue.Latitude,
                    Longitude = matchedVenue.Longitude,
                    Status = matchedVenue.Status,
                    UpdatedBy = updatedBy,
                    DateUpdated = matchedVenue.DateUpdated,
                    VenueID = matchedVenue.VenueID,
                    ProviderID = matchedVenue.ProviderID,
                    ProvVenueID = matchedVenue.ProvVenueID,
                    Email = matchedVenue.Email,
                    Website = matchedVenue.Website,
                    Telephone = matchedVenue.Telephone,
                    CreatedBy = matchedVenue.CreatedBy,
                    CreatedDate = DateTime.Now,
                    LocationId = matchedVenue.LocationId,
                    TribalLocationId = matchedVenue.TribalLocationId
                };
                await documentClient.UpsertDocumentAsync(collectionUri, editedVenue);
            }

            IList<Venue> GetAllOldVenues()
            {
                var list = new List<Venue>();
                var lookupFileResourceName = "Dfc.ProviderPortal.TribalExporter.2020-01-24_0325-venues-backup.json";
                using (var stream = typeof(VenueReferenceChecker).Assembly.GetManifestResourceStream(lookupFileResourceName))
                using (StreamReader file = new StreamReader(stream))
                {
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        while (reader.Read())
                        {
                            JArray o2 = (JArray)JToken.ReadFrom(reader);

                            foreach (var item in o2)
                            {
                                Venue v = item.ToObject<Venue>();
                                list.Add(v);
                            }
                        }
                    }
                }
                return list;
            }
        }
    }
}
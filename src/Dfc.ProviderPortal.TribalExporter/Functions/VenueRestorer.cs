using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Venues;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
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
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var container = configuration["BlobStorageSettings:Container"];
            var whiteListProviders = await GetProviderWhiteList();
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

            //grand totals
            var invalidCourseRunReferences = 0;
            var totalInvalidCourseRunReferences = 0;
            var uniqueInvalidVenues = new HashSet<string>();
            var replacedInvalidVenues = new HashSet<string>();
            var providerList = new HashSet<int>();

            //provider scoped totals
            var totalInvalidApprenticeshipLocationReferences = 0;
            var invalidApprenticeshipLocationReferences = 0;

            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {

                foreach (var ukprn in whiteListProviders)
                {
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
                                    var currentVenue = await GetVenueById(courserun.VenueId?.ToString());
                                    var oldVenue = old_venues.FirstOrDefault(x => new Guid(x.ID) == courserun.VenueId);

                                    //if current venues provider is different to course then attempt to replace it with
                                    //old venue from cosmos backup.
                                    if (currentVenue != null && currentVenue.UKPRN != course.ProviderUKPRN)
                                    {
                                        //replace existing venue with old venue if a match is found
                                        if (oldVenue != null && oldVenue.UKPRN == course.ProviderUKPRN)
                                        {
                                            await ReplaceVenue(currentVenue.ID, oldVenue);
                                            replacedInvalidVenues.Add(currentVenue.ID);
                                        }

                                        //invalid references
                                        providerList.Add(ukprn);
                                        uniqueInvalidVenues.Add(currentVenue.ID);
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
                                if (location.VenueId.HasValue && location.VenueId != Guid.Empty)
                                {
                                    var currentVenue = await GetVenueById(location.VenueId?.ToString());

                                    if (currentVenue != null && currentVenue.UKPRN != location.ProviderUKPRN)
                                    {
                                        var oldVenue = old_venues.FirstOrDefault(x => x.ID == currentVenue.ID);

                                        //replace existing venue with old venue if a match is found
                                        if (oldVenue != null && oldVenue.UKPRN == apprenticeship.ProviderUKPRN)
                                        {
                                            Console.WriteLine($"Invalid Apprenticeship location, apprenticeship should reference {oldVenue.VenueName}");
                                            await ReplaceVenue(currentVenue.ID, oldVenue);
                                            replacedInvalidVenues.Add(currentVenue.ID);
                                        }
                                    }
                                }
                            }
                            totalInvalidApprenticeshipLocationReferences += invalidApprenticeshipLocationReferences;
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message, e);
                        Console.WriteLine("error occurred while fetching data");
                    }

                    //total for provider
                    Console.WriteLine($"{invalidCourseRunReferences} invalid venue references for {ukprn}");
                    Console.WriteLine($"{invalidApprenticeshipLocationReferences} invalid apprenticeship references for {ukprn}");
                    Console.WriteLine($"{providerList.Count()} providers affected");
                }

                foreach (var id in providerList)
                {
                    logCsvWriter.WriteField(id);
                    logCsvWriter.NextRecord();
                }

                // Upload log CSV to blob storage
                {
                    logStreamWriter.Flush();
                    logStream.Seek(0L, SeekOrigin.Begin);
                    var blob = blobhelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                    await blob.UploadFromStreamAsync(logStream);
                }
            }

            //
            Console.WriteLine($"{totalInvalidCourseRunReferences} courserun invalid references in total");
            Console.WriteLine($"{totalInvalidApprenticeshipLocationReferences} apprenticeship location invalid references in total");
            Console.WriteLine($"{replacedInvalidVenues.Count()} venues have been reverted back to old venues");
            Console.WriteLine($"{uniqueInvalidVenues.Count()} Venues were invalid");

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
                    catch (Exception e)
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

            async Task ReplaceVenue(string id, Venue matchedVenue)
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
                    UpdatedBy = matchedVenue.UpdatedBy,
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
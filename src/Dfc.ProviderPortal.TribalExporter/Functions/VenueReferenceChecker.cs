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
    public static class VenueReferenceChecker
    {
        [FunctionName(nameof(VenueReferenceChecker))]
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
            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);
            var apprenticeshipCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var logFileName = $"ProvidersWithInvalidVenueReferences--{DateTime.Now.ToString("dd-MM-yy HHmm")}";

            var result = new List<VenueReference>();
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";

            //counters
            var ukprnsThatFailedToFetchVenues = 0;
            var uniqueInvalidVenues = new HashSet<string>();
            var replacedInvalidVenues = new HashSet<string>();

            var allVenues = GetAllOldVenues();
            //grand totals
            var totalInvalidApprenticeshipLocationReferences = 0;
            var totalInvalidCourseRunReferences = 0;


            //provider scoped totals
            var invalidCourseRunReferences = 0;
            var invalidApprenticeshipLocationReferences = 0;

            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {
                //every provider
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

                                    if (currentVenue != null)
                                    {
                                        if (course.ProviderUKPRN != currentVenue.UKPRN)
                                        {
                                            uniqueInvalidVenues.Add(courserun.VenueId.ToString());
                                            invalidCourseRunReferences++;
                                        }

                                        result.Add(new VenueReference()
                                        {
                                            UKPRN = course.ProviderUKPRN,
                                            VenueId = courserun.VenueId.ToString(),
                                            VenueUKPRN = currentVenue.UKPRN,
                                            Address1 = currentVenue.Address1,
                                            Postcode = currentVenue.PostCode,
                                            VenueName = currentVenue.VenueName,
                                            UKPRNMatched = (course.ProviderUKPRN == currentVenue.UKPRN),
                                            Message = (course.ProviderUKPRN == currentVenue.UKPRN) ? "Venue UKPRN Matches Course UKPRN" : "Venue UKPRN Does not match Course UKPRN",
                                            Type = "Course",
                                            CourseId = course.id,
                                            CourseRunId = courserun.id
                                        });
                                    }
                                    else
                                    {
                                        result.Add(new VenueReference()
                                        {
                                            UKPRN = course.ProviderUKPRN,
                                            UKPRNMatched = false,
                                            VenueUKPRN = -1,
                                            VenueId = courserun.VenueId.ToString(),
                                            Message = "VenueId does not exist in venues",
                                            Type = "Course",
                                            CourseId = course.id,
                                            CourseRunId = courserun.id
                                        });
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

                                    //venue exists in cosmos
                                    if (currentVenue != null)
                                    {
                                        if (location.ProviderUKPRN != currentVenue.UKPRN)
                                        {
                                            uniqueInvalidVenues.Add(location.VenueId.ToString());
                                            invalidApprenticeshipLocationReferences++;
                                        }

                                        //apprenticeshipId
                                        result.Add(new VenueReference()
                                        {
                                            UKPRN = apprenticeship.ProviderUKPRN,
                                            ApprenticeshipLocationUKPRN = location.ProviderUKPRN,
                                            VenueId = location.VenueId.ToString(),
                                            VenueUKPRN = currentVenue.UKPRN,
                                            Address1 = currentVenue.Address1,
                                            Postcode = currentVenue.PostCode,
                                            VenueName = currentVenue.VenueName,
                                            UKPRNMatched = (location.ProviderUKPRN == currentVenue.UKPRN),
                                            Message = (apprenticeship.ProviderUKPRN == currentVenue.UKPRN) ? "Venue UKPRN Matches Apprenticeship UKPRN" : "Venue UKPRN Does not match Apprenticeship UKPRN",
                                            Type = "Apprenticeship",
                                            ApprenticeshipId = apprenticeship.id
                                        });
                                    }
                                    else
                                    {
                                        result.Add(new VenueReference()
                                        {
                                            UKPRN = apprenticeship.ProviderUKPRN,
                                            ApprenticeshipLocationUKPRN = location.ProviderUKPRN,
                                            UKPRNMatched = false,
                                            VenueUKPRN = -1,
                                            VenueId = location.VenueId.ToString(),
                                            Type = "Apprenticeship",
                                            Message = "VenueId does not exist in venues",
                                            ApprenticeshipId = apprenticeship.id
                                        });
                                    }
                                }
                            }
                            totalInvalidApprenticeshipLocationReferences += invalidApprenticeshipLocationReferences;
                        }

                        //total for provider
                        Console.WriteLine($"{invalidCourseRunReferences} invalid venue references for {ukprn}");
                        Console.WriteLine($"{invalidApprenticeshipLocationReferences} invalid apprenticeship references for {ukprn}");
                        Console.WriteLine($"{uniqueInvalidVenues.Count()} unique venues");

                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message, e);
                    }
                }

                //block to try and fetch all venues for every provider
                //to make sure that venues can be fetched without error.
                foreach (var ukprn in whiteListProviders)
                {
                    try
                    {
                        var venues = await GetVenues(ukprn);
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message, e);
                        Console.WriteLine($"{ukprn} - failed to fetch venues");
                        ukprnsThatFailedToFetchVenues++;
                    }
                }

                //write venue reference documents
                logCsvWriter.WriteHeader(typeof(VenueReference));
                logCsvWriter.NextRecord();
                foreach (var id in result)
                {

                    logCsvWriter.WriteRecord(id);
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
            Console.WriteLine($"{ukprnsThatFailedToFetchVenues} ukprns failed to fetch venues");

            async Task<List<Course>> GetCourses(int ukprn)
            {
                var courses = new List<Course>();
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

    public class VenueReference
    {
        public Guid? ApprenticeshipId { get; set; }
        public Guid? CourseRunId { get; set; }
        public Guid? CourseId { get; set; }
        public int? ApprenticeshipLocationUKPRN { get; set; }
        public int UKPRN { get; set; }
        public string VenueId { get; set; }
        public int VenueUKPRN { get; set; }
        public string VenueName { get; set; }
        public string Address1 { get; set; }
        public string Postcode { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public bool UKPRNMatched { get; set; }
    }
}
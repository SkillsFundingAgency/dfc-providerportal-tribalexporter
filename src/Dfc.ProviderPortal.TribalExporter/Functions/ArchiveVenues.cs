using CsvHelper;
using Dfc.CourseDirectory.Models.Helpers;
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ArchiveVenues
    {
        /// <summary>
        /// This function is intended archive venues that are deemed duplicates, and update the the corressponding courses/apprenticeships to reference the
        /// current verion of the venue.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="configuration"></param>
        /// <param name="cosmosDbHelper"></param>
        /// <param name="blobHelper"></param>
        /// <param name="loggerFactory"></param>
        /// <returns></returns>
        [FunctionName(nameof(ArchiveVenues))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IBlobStorageHelper blobHelper,
            [Inject] ILoggerFactory loggerFactory)
        {
            var whitelistFileName = "ProviderWhiteList.txt";
            var venuesCollectionId = "venues";
            var coursesCollectionId = "courses";
            var apprenticeshipCollectionId = "apprenticeship";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var documentClient = cosmosDbHelper.GetClient();
            var updatedBy = "ArchiveVenues";
            var logger = loggerFactory.CreateLogger(typeof(ArchiveCourses));
            var whitelist = await GetProviderWhiteList();
            var venueCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, venuesCollectionId);
            var coursesCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);
            var apprenticeshipCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var totalArchived = 0;
            var totalCoursesReferencingOldVenue = 0;
            var totalApprenticeshipReferenceoldVenue = 0;

            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            {
                // archived venues
                logCsvWriter.WriteField("UKPRN");
                logCsvWriter.WriteField("Archived VenueId");
                logCsvWriter.WriteField("Archived Venue Name");
                logCsvWriter.WriteField("Archived Venue Address1");
                logCsvWriter.WriteField("New VenueId");
                logCsvWriter.WriteField("New Venue Name");
                logCsvWriter.WriteField("New Venue Address1");
                logCsvWriter.WriteField("Course Run Id");
                logCsvWriter.WriteField("ApprenticeshipLocation Id");
                logCsvWriter.WriteField("Message");
                logCsvWriter.WriteField("Type");

                logCsvWriter.NextRecord();

                foreach (var ukprn in whitelist)
                {
                    try
                    {
                        int totalArchivedForProvider = 0;
                        var allVenuesForProvider = await GetVenues(ukprn);
                        var allCoursesForProvider = await GetCourses(ukprn);
                        var allApprenticeshipsForProvider = await GetApprenticeships(ukprn);

                        //identify duplicates
                        var comp = new VenueEqualityComparer();
                        var uniqueGroups = allVenuesForProvider.GroupBy(x => x, comp);

                        //archive duplicate venues
                        foreach (var item in uniqueGroups)
                        {
                            //tribal venues & trival locations when venues were migrated, both locations and & venues from tribal 
                            //were migrated as seperate records even though the address was the same. The below attempts to merge the two.
                            var migratedVenues = item.ToList().Where(x => x.CreatedBy == "VenueMigrator" && x.UpdatedBy != updatedBy); //expecting more than one here.

                            var tribalLocationVenue = migratedVenues.FirstOrDefault(x => x.LocationId != null);
                            var tribalVenue = migratedVenues.FirstOrDefault(x => x.VenueID != 0);
                            var nonCurrentVenues = item.ToList().Where(x => x.CreatedBy != "VenueMigrator").ToList();
                            var currentVenue = MergeVenue(tribalLocationVenue, tribalVenue, out string venueType);

                            //skip group if there is no current venue
                            if (currentVenue == null)
                                continue;

                            //if there is a location venue & venue, add venue to list of non current venues
                            //and update the currentVenue to indicate it has been merged.
                            if (venueType == "Both")
                            {
                                nonCurrentVenues.Add(tribalVenue);

                                await ReplaceMergedREcord(currentVenue);
                            }

                            //courses that have course runs with old venue references.
                            var courseRunsOldVenue = allCoursesForProvider.Where(p => p.CourseRuns.Any(x => nonCurrentVenues.Where(y => Guid.Parse(y.ID) == x.VenueId).Count() > 0)).ToList();
                            totalCoursesReferencingOldVenue += courseRunsOldVenue.Count();

                            //apprenticeships that have locations with old venue refe
                            var apprenticeshipsOldVenue = allApprenticeshipsForProvider.Where(p => p.ApprenticeshipLocations.Any(x => nonCurrentVenues.Where(y => Guid.Parse(y.ID) == x.LocationGuidId).Count() > 0)).ToList();
                            totalApprenticeshipReferenceoldVenue += apprenticeshipsOldVenue.Count();

                            Console.WriteLine($"Archiving {nonCurrentVenues.Count()} - {ukprn} - {currentVenue.Address1}");

                            //handle archiving venue
                            foreach (var archivingVenue in nonCurrentVenues)
                            {
                                await ArchiveVenue(archivingVenue, ukprn);

                                logCsvWriter.WriteField(ukprn);
                                logCsvWriter.WriteField(archivingVenue.ID);
                                logCsvWriter.WriteField(archivingVenue.VenueName);
                                logCsvWriter.WriteField($"{archivingVenue.Address1},{archivingVenue.Address2}, {archivingVenue.PostCode}");
                                logCsvWriter.WriteField(currentVenue.ID);
                                logCsvWriter.WriteField(currentVenue.VenueName);
                                logCsvWriter.WriteField($"{currentVenue.Address1},{currentVenue.Address2}, {currentVenue.PostCode}");
                                logCsvWriter.WriteField("");
                                logCsvWriter.WriteField(""); //ApprenticeshipLocationId
                                logCsvWriter.WriteField($"There were {nonCurrentVenues.Count()} duplicate Venues");
                                logCsvWriter.WriteField("Venue");
                                logCsvWriter.NextRecord();

                                totalArchived++;
                                totalArchivedForProvider++;

                                //update courses that reference old venues
                                foreach (var course in courseRunsOldVenue)
                                {
                                    //update venue to point at new venue.
                                    course.CourseRuns.Where(p => nonCurrentVenues.Any(y => Guid.Parse(y.ID) == p.VenueId))
                                                                                 .ToList()
                                                                                 .ForEach(x =>
                                                                                 {
                                                                                     //update course instance
                                                                                     x.VenueId = Guid.Parse(currentVenue.ID);
                                                                                     x.UpdatedBy = updatedBy;

                                                                                     //log change
                                                                                     logCsvWriter.WriteField(ukprn);
                                                                                     logCsvWriter.WriteField(archivingVenue.ID);
                                                                                     logCsvWriter.WriteField(archivingVenue.VenueName);
                                                                                     logCsvWriter.WriteField($"{archivingVenue.Address1},{archivingVenue.Address2}, {archivingVenue.PostCode}");
                                                                                     logCsvWriter.WriteField(currentVenue.ID);
                                                                                     logCsvWriter.WriteField(currentVenue.VenueName);
                                                                                     logCsvWriter.WriteField($"{currentVenue.Address1},{currentVenue.Address2}, {currentVenue.PostCode}");
                                                                                     logCsvWriter.WriteField(x.CourseInstanceId);
                                                                                     logCsvWriter.WriteField(""); //ApprenticeshipLocationId
                                                                                     logCsvWriter.WriteField($"There were {nonCurrentVenues.Count()} duplicate Venues");
                                                                                     logCsvWriter.WriteField("Course");
                                                                                     logCsvWriter.NextRecord();
                                                                                 });

                                    //update venue to reference currentVenue
                                    var coursedocumentLink = UriFactory.CreateDocumentUri(databaseId, coursesCollectionId, course.id.ToString());
                                    await documentClient.ReplaceDocumentAsync(coursedocumentLink, course, new RequestOptions()
                                    {
                                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                                    });
                                }

                                //update courses that reference old venues
                                foreach (var apprenticeship in apprenticeshipsOldVenue)
                                {
                                    //update venue to point at new venue for locations
                                    apprenticeship.ApprenticeshipLocations.Where(p => nonCurrentVenues.Any(y => Guid.Parse(y.ID) == p.LocationGuidId))
                                                                                                      .ToList()
                                                                                                      .ForEach(x =>
                                                                                                      {
                                                                                                          //update apprenticeship location
                                                                                                          x.LocationGuidId = Guid.Parse(currentVenue.ID);
                                                                                                          x.UpdatedBy = updatedBy;

                                                                                                          //log change
                                                                                                          logCsvWriter.WriteField(ukprn);
                                                                                                          logCsvWriter.WriteField(archivingVenue.ID);
                                                                                                          logCsvWriter.WriteField(archivingVenue.VenueName);
                                                                                                          logCsvWriter.WriteField($"{archivingVenue.Address1},{archivingVenue.Address2}, {archivingVenue.PostCode}");
                                                                                                          logCsvWriter.WriteField(currentVenue.ID);
                                                                                                          logCsvWriter.WriteField(currentVenue.VenueName);
                                                                                                          logCsvWriter.WriteField($"{currentVenue.Address1},{currentVenue.Address2}, {currentVenue.PostCode}");
                                                                                                          logCsvWriter.WriteField(""); //Course Instance
                                                                                                          logCsvWriter.WriteField(x.Id);
                                                                                                          logCsvWriter.WriteField($"There were {nonCurrentVenues.Count()} duplicate Venues");
                                                                                                          logCsvWriter.WriteField("Apprenticeship");
                                                                                                          logCsvWriter.NextRecord();
                                                                                                      });


                                    //update apprenticeship to reference currentvenue
                                    var apprenticeshipDocumentLink = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, apprenticeship.id.ToString());
                                    await documentClient.ReplaceDocumentAsync(apprenticeshipDocumentLink, apprenticeship, new RequestOptions());
                                }
                            }

                        }
                        Console.WriteLine($"Archived {totalArchivedForProvider} Venues for {ukprn}");
                        logger.LogInformation($"Archived {totalArchivedForProvider} Venues for {ukprn}");

                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.Message);
                    }
                }

                // Upload log CSV to blob storage
                {
                    logStreamWriter.Flush();

                    logStream.Seek(0L, SeekOrigin.Begin);

                    var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference("ArchivedVenues");
                    await blob.UploadFromStreamAsync(logStream);
                }
            }


            Console.WriteLine($"Total Course runs that reference an old venue: {totalCoursesReferencingOldVenue}");
            Console.WriteLine($"Total Apparenticeships that reference an old Venue {totalApprenticeshipReferenceoldVenue}");
            Console.WriteLine($"Total Archived Venues {totalArchived}");

            async Task ArchiveVenue(Venue archivingVenue, int ukprn)
            {
                //archive venue
                archivingVenue.Status = VenueStatus.Archived;
                archivingVenue.UpdatedBy = updatedBy;
                archivingVenue.DateUpdated = DateTime.Now;
                var documentLink = UriFactory.CreateDocumentUri(databaseId, venuesCollectionId, archivingVenue.ID.ToString());
                await documentClient.ReplaceDocumentAsync(documentLink, archivingVenue, new RequestOptions());
            }

            async Task ReplaceMergedREcord(Venue mergedRecord)
            {
                //archive venue
                mergedRecord.UpdatedBy = updatedBy;
                mergedRecord.DateUpdated = DateTime.Now;
                var documentLink = UriFactory.CreateDocumentUri(databaseId, venuesCollectionId, mergedRecord.ID.ToString());
                await documentClient.ReplaceDocumentAsync(documentLink, mergedRecord, new RequestOptions());
            }


            Venue MergeVenue(Venue locationVenue, Venue venue, out string selectedVenue)
            {

                //default to first none null venue, location is chosen first.
                var ven = locationVenue ?? venue;

                if (locationVenue != null && venue != null)
                    selectedVenue = "Both";
                else if (locationVenue == null && venue != null)
                    selectedVenue = "Venue";
                else if (locationVenue != null && venue == null)
                    selectedVenue = "Location";
                else
                    selectedVenue = "None";

                //if there are two venues, one with a venue id & one with a location id. 
                //merge them.
                if (locationVenue != null && venue != null)
                {
                    ven.VenueID = venue.VenueID;
                }

                return ven;
            }

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
                        .Where(p => p.UKPRN == ukprn && p.Status == VenueStatus.Live)
                        .AsDocumentQuery()
                        .ExecuteNextAsync<Venue>();

                    venues.AddRange(queryResponse.ToList());

                    continuation = queryResponse.ResponseContinuation;
                }
                while (continuation != null);
                return venues;
            }

            async Task<List<Apprenticeship>> GetApprenticeships(int ukprn)
            {
                var apprenticeships = new List<Apprenticeship>();
                string continuation = null;
                //get all apprenticeships for provider
                do
                {
                    var feedOptions = new FeedOptions()
                    {
                        RequestContinuation = continuation,
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    };

                    //try/catch required as there are Apprenticeship records that are not valid (venueId is null in cosmos).
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
                var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(whitelistFileName);

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
        }
    }
}

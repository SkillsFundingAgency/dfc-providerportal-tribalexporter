using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CsvHelper;
using Dapper;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Regions;
using Dfc.CourseDirectory.Services;
using Dfc.CourseDirectory.Services.Interfaces;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class CourseMigrator
    {
        private enum CourseMigrationResult { SkippedDueToErrors, Inserted, Updated, Exception }

        [FunctionName(nameof(CourseMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
            string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
            [Inject] IConfigurationRoot configuration,
            [Inject] ICosmosDbHelper cosmosDbHelper,
            [Inject] IVenueCollectionService venueCollectionService,
            [Inject] ILarsSearchService larsSearchService,
            [Inject] IBlobStorageHelper blobHelper)
        {
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var coursesCollectionId = "courses";
            var logFileName = $"CourseMigrator-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var blobContainer = configuration["BlobStorageSettings:Container"];
            var whitelistFileName = "ProviderWhiteList.txt";

            var connectionString = configuration.GetConnectionString("TribalRestore");

            var cosmosDbClient = cosmosDbHelper.GetClient();
            using (var logStream = new MemoryStream())
            using (var logStreamWriter = new StreamWriter(logStream))
            using (var logCsvWriter = new CsvWriter(logStreamWriter, CultureInfo.InvariantCulture))
            using (var conn1 = new SqlConnection(connectionString))
            using (var conn2 = new SqlConnection(connectionString))
            {
                // Log CSV headers
                logCsvWriter.WriteField("CourseId");
                logCsvWriter.WriteField("UKPRN");
                logCsvWriter.WriteField("Success");
                logCsvWriter.WriteField("Status");
                logCsvWriter.WriteField("Course instances");
                logCsvWriter.WriteField("Error list");
                logCsvWriter.NextRecord();

                var whitelist = await GetProviderWhiteList();

                await conn1.OpenAsync();
                await conn2.OpenAsync();

                using (var coursesCmd = conn1.CreateCommand())
                using (var coursesInstancesCmd = conn2.CreateCommand())
                {
                    coursesCmd.CommandTimeout = 60 * 60;  // 1 hour
                    coursesInstancesCmd.CommandTimeout = 60 * 60;  // 1 hour

                    coursesCmd.CommandText = @"
SELECT
    c.CourseId,
    c.CourseTitle,
    c.CourseSummary,
    c.LearningAimRefId,
    c.QualificationLevelId,
    c.EntryRequirements,
    c.ProviderOwnCourseRef,
    c.Url,
    p.UKPRN,
    c.EquipmentRequired,
    c.AssessmentMethod,
    p.Loans24Plus
FROM Course c
JOIN Provider p ON c.ProviderId = p.ProviderId
WHERE c.RecordStatusId = 2  --Live
--Last updated within 24 months of data freeze 28/02
AND (c.ModifiedDateTimeUtc >= '2018-02-28' OR EXISTS (
    SELECT 1 FROM CourseInstance ci
    WHERE ci.CourseId = c.CourseId
    AND ci.RecordStatusId = 2
    AND ci.ModifiedDateTimeUtc >= '2018-02-28'
))
ORDER BY c.CourseId, c.ProviderId";

                    coursesInstancesCmd.CommandText = @"
SELECT
    ci.CourseInstanceId,
    ci.CourseId,
    ci.ProviderOwnCourseInstanceRef,
    ci.StudyModeId,
    ci.AttendanceTypeId,
    ci.AttendancePatternId,
    ci.DurationUnit,
    ci.DurationUnitId,
    ci.DurationAsText,
    ci.StartDateDescription,
	cisd.StartDate,
    ci.Price,
    ci.PriceAsText,
    ci.Url,
    civ.VenueId,
    ci.VenueLocationId
FROM CourseInstance ci
LEFT JOIN CourseInstanceVenue civ ON ci.CourseInstanceId = civ.CourseInstanceId
LEFT JOIN CourseInstanceStartDate cisd ON ci.CourseInstanceId = cisd.CourseInstanceId
WHERE ci.RecordStatusId = 2  --Live
ORDER BY ci.CourseId, ci.OfferedByProviderId";

                    using (var coursesReader = coursesCmd.ExecuteReader())
                    using (var courseInstanceReader = coursesInstancesCmd.ExecuteReader())
                    {
                        var instanceReader = new CourseInstanceReader(courseInstanceReader);
                        var instanceEnumerator = instanceReader.ProcessReader().GetEnumerator();
                        var courseRowReader = coursesReader.GetRowParser<CourseResult>();

                        while (await coursesReader.ReadAsync())
                        {
                            var course = courseRowReader(coursesReader);

                            // If provider is not on whitelist - skip this course
                            if (!whitelist.Contains(course.UKPRN))
                            {
                                continue;
                            }

                            var instances = instanceReader.ConsumeReader(instanceEnumerator, course.CourseId);

                            var errors = new List<string>();
                            CourseMigrationResult result;

                            try
                            {

                                // Tribal don't have any Courses with zero CourseInstances...
                                if (instances.Count == 0)
                                {
                                    errors.Add("Found zero CourseInstances.");
                                }

                                // Check LARS
                                var larsSearchResults = !string.IsNullOrEmpty(course.LearningAimRefId) ?
                                    await QueryLars(course.LearningAimRefId) :
                                    Array.Empty<LarsSearchResultItem>();

                                // Check the venues exist
                                Dictionary<int, Guid> venueIdMap = new Dictionary<int, Guid>();
                                foreach (var venueId in instances.Where(i => i.VenueId.HasValue).Select(i => i.VenueId.Value))
                                {
                                    if (venueIdMap.ContainsKey(venueId))
                                    {
                                        continue;
                                    }

                                    var cosmosVenue = await venueCollectionService.GetDocumentByVenueId(venueId);

                                    if (cosmosVenue == null)
                                    {
                                        errors.Add($"Missing venue {venueId}.");
                                    }
                                    else
                                    {
                                        venueIdMap.Add(venueId, Guid.Parse(cosmosVenue.ID));
                                    }
                                }

                                if (errors.Count == 0)
                                {
                                    // Got the course in Cosmos already?
                                    var existingCourseRecord = await GetExistingCourse(course.CourseId, course.UKPRN, cosmosDbClient);

                                    var mappedCourseRuns = instances
                                        .Select(i =>
                                        {
                                            Guid? venueId = null;
                                            if (i.VenueId.HasValue)
                                            {
                                                venueId = venueIdMap[i.VenueId.Value];
                                            }

                                                // Retain the existing Cosmos ID if there is one
                                                // N.B. We can have more than one match on CourseInstanceId since we 'explode' on multiple start dates
                                                var courseRunId =
                                                        existingCourseRecord?.CourseRuns.SingleOrDefault(r => r.CourseInstanceId == i.CourseInstanceId && r.StartDate == i.StartDate)?.id ??
                                                        Guid.NewGuid();

                                            return MapCourseInstance(course, i, courseRunId, venueId, errors);
                                        })
                                        .ToList();

                                    var courseId = existingCourseRecord?.id ?? Guid.NewGuid();
                                    var mappedCourse = MapCourse(course, mappedCourseRuns, larsSearchResults, courseId, errors);

                                    var added = await UpsertCourse(mappedCourse, cosmosDbClient);
                                    result = added ? CourseMigrationResult.Inserted : CourseMigrationResult.Updated;
                                }
                                else
                                {
                                    result = CourseMigrationResult.SkippedDueToErrors;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add(ex.ToString().Replace("\n", " "));
                                result = CourseMigrationResult.Exception;
                            }

                            // Write to log
                            logCsvWriter.WriteField(course.CourseId);
                            logCsvWriter.WriteField(course.UKPRN);
                            logCsvWriter.WriteField(result == CourseMigrationResult.Inserted || result == CourseMigrationResult.Updated);
                            logCsvWriter.WriteField(result.ToString());
                            logCsvWriter.WriteField(instances.Count);
                            logCsvWriter.WriteField(string.Join(", ", errors));
                            logCsvWriter.NextRecord();
                        }
                    }
                }

                // Upload log CSV to blob storage
                {
                    logStreamWriter.Flush();

                    logStream.Seek(0L, SeekOrigin.Begin);

                    var blob = blobHelper.GetBlobContainer(blobContainer).GetBlockBlobReference(logFileName);
                    await blob.UploadFromStreamAsync(logStream);
                }
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

            async Task<IReadOnlyCollection<LarsSearchResultItem>> QueryLars(string learningAimRef)
            {
                var result = await larsSearchService.SearchAsync(new LarsSearchCriteria(learningAimRef, top: 1, skip: 0));

                if (result.IsFailure)
                {
                    throw new Exception($"LARS search failed:\n{result.Error}");
                }

                return result.Value.Value.ToList();
            }

            async Task<bool> UpsertCourse(Course course, DocumentClient documentClient)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);

                var result = await documentClient.UpsertDocumentAsync(collectionLink, course, new RequestOptions()
                {
                    PartitionKey = new Microsoft.Azure.Documents.PartitionKey(course.ProviderUKPRN)
                });

                return result.StatusCode == HttpStatusCode.Created;
            }

            async Task<Course> GetExistingCourse(int courseId, int ukprn, DocumentClient documentClient)
            {
                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, coursesCollectionId);

                var query = documentClient
                    .CreateDocumentQuery<Course>(collectionLink, new FeedOptions()
                    {
                        PartitionKey = new Microsoft.Azure.Documents.PartitionKey(ukprn)
                    })
                    .Where(d => d.CourseId == courseId)
                    .AsDocumentQuery();

                return (await query.ExecuteNextAsync()).FirstOrDefault();
            }

            AttendancePattern MapAttendancePattern(DeliveryMode deliveryMode, int? attendancePatternId, out bool hasError)
            {
                if (deliveryMode != DeliveryMode.ClassroomBased)
                {
                    hasError = false;
                    return AttendancePattern.Undefined;
                }

                if (!attendancePatternId.HasValue)
                {
                    hasError = true;
                    return AttendancePattern.Undefined;
                }

                switch (attendancePatternId.Value)
                {
                    case 1:
                        hasError = false;
                        return AttendancePattern.Daytime;
                    case 2:
                        hasError = false;
                        return AttendancePattern.DayOrBlockRelease;
                    case 3:
                    case 4:
                        hasError = false;
                        return AttendancePattern.Evening;
                    case 5:
                        hasError = false;
                        return AttendancePattern.Weekend;
                    case 6:
                    case 7:
                    case 8:
                    default:
                        hasError = true;
                        return AttendancePattern.Undefined;
                }
            }

            DeliveryMode MapDeliveryMode(int? attendanceTypeId, out bool hasError)
            {
                if (!attendanceTypeId.HasValue)
                {
                    hasError = true;
                    return DeliveryMode.Undefined;
                }

                switch (attendanceTypeId.Value)
                {
                    case 1:
                        hasError = false;
                        return DeliveryMode.ClassroomBased;
                    case 2:
                    case 3:
                        hasError = false;
                        return DeliveryMode.WorkBased;
                    case 7:
                    case 8:
                        hasError = false;
                        return DeliveryMode.Online;
                    case 4:
                    case 5:
                    case 6:
                    case 9:
                    default:
                        hasError = true;
                        return DeliveryMode.Undefined;
                }
            }

            StudyMode MapStudyMode(DeliveryMode deliveryMode, int? studyModeId, out bool hasError)
            {
                if (deliveryMode != DeliveryMode.ClassroomBased)
                {
                    hasError = false;
                    return StudyMode.Undefined;
                }

                if (!studyModeId.HasValue)
                {
                    hasError = true;
                    return StudyMode.Undefined;
                }

                switch (studyModeId.Value)
                {
                    case 1:
                        hasError = false;
                        return StudyMode.FullTime;
                    case 2:
                        hasError = false;
                        return StudyMode.PartTime;
                    case 3:
                        hasError = true;
                        return StudyMode.Undefined;
                    case 4:
                        hasError = false;
                        return StudyMode.Flexible;
                    default:
                        hasError = true;
                        return StudyMode.Undefined;
                }
            }

            (DurationUnit, int?) MapDuration(int? durationUnit, int? durationValue, out bool hasError)
            {
                if (!durationUnit.HasValue)
                {
                    hasError = false;
                    return (DurationUnit.Undefined, null);
                }

                switch (durationUnit.Value)
                {
                    case 1:
                        hasError = false;
                        return (DurationUnit.Hours, durationValue);
                    case 2:
                        hasError = false;
                        return (DurationUnit.Days, durationValue);
                    case 3:
                        hasError = false;
                        return (DurationUnit.Weeks, durationValue);
                    case 4:
                        hasError = false;
                        return (DurationUnit.Months, durationValue);
                    case 5:
                        hasError = false;
                        return (DurationUnit.Months, 3);
                    case 7:
                        hasError = false;
                        return (DurationUnit.Years, durationValue);
                    case 6:
                    default:
                        hasError = true;
                        return (DurationUnit.Undefined, null);
                }
            }

            CourseRun MapCourseInstance(
                CourseResult course,
                CourseInstanceResult courseInstance,
                Guid id,
                Guid? venueId,
                List<string> errors)
            {
                var deliveryMode = MapDeliveryMode(courseInstance.AttendanceTypeId, out var deliveryModeError);
                var attendancePattern = MapAttendancePattern(deliveryMode, courseInstance.AttendancePatternId, out var attendancePatternError);
                var studyMode = MapStudyMode(deliveryMode, courseInstance.StudyModeId, out var studyModeError);
                var (durationUnit, durationValue) = MapDuration(courseInstance.DurationUnitId, courseInstance.DurationUnit, out var durationError);

                var hasErrors = false;

                if (attendancePatternError)
                {
                    errors.Add($"Invalid AttendancePattern");
                    hasErrors = true;
                }

                if (deliveryModeError)
                {
                    errors.Add($"Invalid DeliveryMode");
                    hasErrors = true;
                }

                if (studyModeError)
                {
                    errors.Add($"Invalid StudyMode");
                    hasErrors = true;
                }

                if (durationError)
                {
                    errors.Add($"Invalid Duration");
                    hasErrors = true;
                }

                bool flexibleStartDate = default;
                DateTime? startDate = default;

                if (deliveryMode == DeliveryMode.Online)
                {
                    flexibleStartDate = true;
                }
                else if (courseInstance.StartDate.HasValue)
                {
                    flexibleStartDate = false;
                    startDate = courseInstance.StartDate;
                }
                else if (string.IsNullOrEmpty(courseInstance.StartDateDescription))
                {
                    errors.Add($"Empty StartDateDescription");
                    hasErrors = true;
                }
                else if (DateTime.TryParseExact(courseInstance.StartDateDescription, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                {
                    flexibleStartDate = false;
                    startDate = sd;
                }
                else
                {
                    flexibleStartDate = true;
                }

                if (deliveryMode == DeliveryMode.ClassroomBased && !venueId.HasValue)
                {
                    errors.Add($"No venue");
                    hasErrors = true;
                }

                // Work-based should have regions(s) or be national
                bool? national = null;
                IEnumerable<string> regions = Array.Empty<string>();
                IEnumerable<SubRegionItemModel> subRegions = Array.Empty<SubRegionItemModel>();
                if (deliveryMode == DeliveryMode.WorkBased)
                {
                    if (!courseInstance.VenueLocationId.HasValue)
                    {
                        errors.Add("No region found");
                        hasErrors = true;
                    }
                    else
                    {
                        if (RegionLookup.IsNational(courseInstance.VenueLocationId.Value))
                        {
                            national = true;
                        }
                        else
                        {
                            var lookupResult = RegionLookup.FindRegions(courseInstance.VenueLocationId.Value);

                            if (!lookupResult.HasValue)
                            {
                                errors.Add($"Cannot find sub-region(s) for VenueLocationId {courseInstance.VenueLocationId.Value}");
                                hasErrors = true;
                            }
                            else
                            {
                                regions = lookupResult.Value.regionIds;
                                subRegions = lookupResult.Value.subRegions;
                                national = false;
                            }
                        }
                    }
                }

                var recordStatus = hasErrors ? RecordStatus.MigrationPending : RecordStatus.Live;

                return new CourseRun()
                {
                    AttendancePattern = attendancePattern,
                    Cost = courseInstance.Price,
                    CostDescription = courseInstance.PriceAsText,
                    CourseInstanceId = courseInstance.CourseInstanceId,
                    CourseName = course.CourseTitle,
                    CourseURL = courseInstance.Url,
                    CreatedBy = "CourseMigrator",
                    CreatedDate = DateTime.Now,
                    DeliveryMode = deliveryMode,
                    DurationUnit = durationUnit,
                    DurationValue = durationValue,
                    FlexibleStartDate = flexibleStartDate,
                    id = id,
                    ProviderCourseID = courseInstance.ProviderOwnCourseInstanceRef,
                    RecordStatus = recordStatus,
                    National = national,
                    Regions = regions,
                    StartDate = startDate,
                    StudyMode = studyMode,
                    SubRegions = subRegions,
                    //UpdatedBy
                    UpdatedDate = DateTime.Now,
                    VenueId = venueId
                };
            }

            Course MapCourse(
                CourseResult course,
                IReadOnlyCollection<CourseRun> courseRuns,
                IReadOnlyCollection<LarsSearchResultItem> larsSearchResults,
                Guid id,
                List<string> errors)
            {
                var isValid = courseRuns.All(r => r.RecordStatus.HasFlag(RecordStatus.Live));

                LarlessReason? larlessReason = string.IsNullOrEmpty(course.LearningAimRefId) ?
                    LarlessReason.NoLars : larsSearchResults.Count == 0 ?
                    LarlessReason.UnknownLars : larsSearchResults.Count > 1 ?
                    LarlessReason.MultipleMatchingLars : // TODO Consider expired LARS
                    LarlessReason.Undefined;

                var qualification = larsSearchResults.Count == 1 ? larsSearchResults.Single() : null;

                if (qualification == null)
                {
                    foreach (var cr in courseRuns)
                    {
                        cr.RecordStatus = RecordStatus.MigrationPending;
                    }

                    errors.Add("LARS lookup failed");
                    isValid = false;
                }

                return new Course()
                {
                    AdultEducationBudget = default,
                    AdvancedLearnerLoan = course.Loans24Plus,
                    AwardOrgCode = qualification?.AwardOrgCode,
                    CourseDescription = course.CourseSummary,
                    CourseId = course.CourseId,
                    CourseRuns = courseRuns,
                    CreatedBy = "CourseMigrator",
                    CreatedDate = DateTime.Now,
                    EntryRequirements = course.EntryRequirements,
                    HowYoullBeAssessed = course.AssessmentMethod,
                    HowYoullLearn = null,
                    id = id,
                    IsValid = isValid,
                    LarlessReason = larlessReason,
                    LearnAimRef = course.LearningAimRefId,
                    NotionalNVQLevelv2 = qualification?.NotionalNVQLevelv2,
                    ProviderUKPRN = course.UKPRN,
                    QualificationCourseTitle = qualification?.LearnAimRefTitle,
                    QualificationType = qualification?.LearnAimRefTypeDesc,
                    //UpdatedBy
                    UpdatedDate = DateTime.Now,
                    WhatYoullLearn = null,
                    WhatYoullNeed = course.EquipmentRequired,
                    WhereNext = null
                };
            }
        }

        /// <summary>
        /// Consumes a DataReader and reads contiguous rows that have the same Course ID.
        /// </summary>
        private class CourseInstanceReader
        {
            private readonly SqlDataReader _reader;
            private readonly Func<SqlDataReader, CourseInstanceResult> _rowParser;

            public CourseInstanceReader(SqlDataReader reader)
            {
                _reader = reader;
                _rowParser = _reader.GetRowParser<CourseInstanceResult>();
            }

            public IEnumerable<CourseInstanceResult> ProcessReader()
            {
                while (_reader.Read())
                {
                    yield return _rowParser(_reader);
                }
            }

            public IReadOnlyCollection<CourseInstanceResult> ConsumeReader(
                IEnumerator<CourseInstanceResult> enumerator,
                int courseId)
            {
                var buffer = new List<CourseInstanceResult>();

                if (enumerator.Current == null)
                {
                    enumerator.MoveNext();
                }

                do
                {
                    var item = enumerator.Current;

                    if (item.CourseId > courseId)
                    {
                        return buffer;
                    }
                    else if (item.CourseId == courseId)
                    {
                        buffer.Add(item);
                    }
                }
                while (enumerator.MoveNext());

                return buffer;
            }
        }

        private class CourseResult
        {
            public int CourseId { get; set; }
            public string CourseTitle { get; set; }
            public string CourseSummary { get; set; }
            public string LearningAimRefId { get; set; }
            public int? QualificationLevelId { get; set; }
            public string EntryRequirements { get; set; }
            public string ProviderOwnCourseRef { get; set; }
            public string Url { get; set; }
            public int UKPRN { get; set; }
            public string EquipmentRequired { get; set; }
            public string AssessmentMethod { get; set; }
            public bool Loans24Plus { get; set; }
        }

        private class CourseInstanceResult
        {
            public int CourseInstanceId { get; set; }
            public int CourseId { get; set; }
            public string ProviderOwnCourseInstanceRef { get; set; }
            public int? StudyModeId { get; set; }
            public int? AttendanceTypeId { get; set; }
            public int? AttendancePatternId { get; set; }
            public int? DurationUnit { get; set; }
            public int? DurationUnitId { get; set; }
            public string DurationAsText { get; set; }
            public decimal? Price { get; set; }
            public string StartDateDescription { get; set; }
            public DateTime? StartDate { get; set; }
            public string PriceAsText { get; set; }
            public string Url { get; set; }
            public int? VenueId { get; set; }
            public int? VenueLocationId { get; set; }
        }
    }
}

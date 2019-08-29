using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Dfc.CourseDirectory.Models.Enums
{
    //[Flags]
    public enum RecordStatus
    {
        [Description("Undefined")]
        Undefined = 0,       
        [Description("Live")]
        Live = 1,
        [Description("Pending")]
        Pending = 2,
        [Description("Archived")]
        Archived = 4,
        [Description("Deleted")]
        Deleted = 8,
        [Description("BulkUload Pending")]
        BulkUloadPending = 16,
        [Description("BulkUpload Ready To Go Live")]
        BulkUploadReadyToGoLive = 32,
        [Description("API Pending")]
        APIPending = 64,
        [Description("API Ready To Go Live")]
        APIReadyToGoLive = 128,
        [Description("Migration Pending")]
        MigrationPending = 256,
        [Description("Migration Ready To Go Live")]
        MigrationReadyToGoLive = 512,
        [Description("LARSless")]
        LARSless = 1024
    }

    public enum TribalRecordStatus
    {
        Undefined = 0,
        Pending = 1,
        Live = 2,
        Archived =3,
        Deleted = 4
    }

    public enum TransferMethod
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("BulkUpload")]
        BulkUpload = 1,
        [Description("API")]
        API = 2,
        [Description("CourseMigrationTool")]
        CourseMigrationTool = 3,
        [Description("CourseMigrationToolCsvFile")]
        CourseMigrationToolCsvFile = 4,
        [Description("CourseMigrationToolSingleUkprn")]
        CourseMigrationToolSingleUkprn = 5
    }

    public enum MigrationSuccess
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Success")]
        Success = 1,
        [Description("Failure")]
        Failure = 2
    }

    public enum DeploymentEnvironment
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Local")]
        Local = 1,
        [Description("Dev")]
        Dev = 2,
        [Description("Sit")]
        Sit = 3,
        [Description("PreProd")]
        PreProd = 4,
        [Description("Prod")]
        Prod = 5
    }

    public enum ValidationMode
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Add CourseRun")]
        AddCourseRun = 1,
        [Description("Edit Course YC")]
        EditCourseYC = 2,
        [Description("Edit Course BU")]
        EditCourseBU = 3,
        [Description("Edit Course MT")]
        EditCourseMT = 4,
        [Description("Copy CourseRun")]
        CopyCourseRun = 5,
        [Description("Bulk Upload Course")]
        BulkUploadCourse = 6,
        [Description("Migrate Course")]
        MigrateCourse = 7,
    }

    public enum LocationType
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Venue")]
        Venue = 1,
        [Description("Region")]
        Region = 2,
        [Description("SubRegion")]
        SubRegion = 3
    }

    public class Enums
    {
    }
}

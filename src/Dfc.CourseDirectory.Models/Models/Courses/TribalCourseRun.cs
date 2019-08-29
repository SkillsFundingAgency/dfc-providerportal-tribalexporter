using System;
using Dfc.CourseDirectory.Models.Interfaces.Courses;
using System.ComponentModel;
using Dfc.CourseDirectory.Models.Enums;

namespace Dfc.CourseDirectory.Models.Models.Courses
{
    //public enum DeliveryMode
    //{
    //    [Description("Undefined")]
    //    Undefined = 0,
    //    [Description("Classroom based")]
    //    ClassroomBased = 1,
    //    [Description("Online")]
    //    Online = 2,
    //    [Description("Work based")]
    //    WorkBased = 3
    //}

    public enum AttendanceType
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Location / campus")]
        Location = 1,
        [Description("Face-to-face (non-campus)")]
        FaceToFaceNonCampus = 2,
        [Description("Work-based")]
        WorkBased = 3,
        [Description("Mixed mode")]
        MixedMode = 4,
        [Description("Distance with attendance")]
        DistanceWithAttendance = 5,
        [Description("Distance without attendance")]
        DistanceWithoutAttendance = 6,
        [Description("Online without attendance")]
        OnlineWithoutAttendance = 7,
        [Description("Online with attendance")]
        OnlineWithAttendance = 8,
        [Description("Not known")]
        NotKnown = 9
    }


    //public enum DurationUnit
    //{
    //    [Description("Undefined")]
    //    Undefined = 0,
    //    [Description("Days")]
    //    Days = 1,
    //    [Description("Weeks")]
    //    Weeks = 2,
    //    [Description("Months")]
    //    Months = 3,
    //    [Description("Years")]
    //    Years = 4
    //}

    public enum TribalDurationUnit
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Hours")]
        Hours = 1,
        [Description("Days")]
        Days = 2,
        [Description("Weeks")]
        Weeks = 3,
        [Description("Months")]
        Months = 4,
        [Description("Terms")]
        Terms = 5,
        [Description("Semesters")]
        Semesters = 6,
        [Description("Years")]
        Years = 7
    }

    //public enum StudyMode
    //{
    //    [Description("Undefined")]
    //    Undefined = 0,
    //    [Description("Full-time")]
    //    FullTime = 1,
    //    [Description("Part-time")]
    //    PartTime = 2,
    //    [Description("Flexible")]
    //    Flexible = 3
    //}

    public enum TribalStudyMode
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Full-time")]
        FullTime = 1,
        [Description("Part-time")]
        PartTime = 2,
        [Description("Part of a full-time program")]
        PartOfAFulltimeProgram = 3,
        [Description("Flexible")]
        Flexible = 4,
        [Description("Not known")]
        NotKnown = 5,
    }

    //public enum AttendancePattern
    //{
    //    [Description("Undefined")]
    //    Undefined = 0,
    //    [Description("Daytime")]
    //    Daytime = 1,
    //    [Description("Evening")]
    //    Evening = 2,
    //    [Description("Weekend")]
    //    Weekend = 3,
    //    [Description("Day/Block Release")]
    //    DayOrBlockRelease = 4
    //}

    public enum TribalAttendancePattern
    {
        [Description("Undefined")]
        Undefined = 0,
        [Description("Daytime/working hours")]
        DaytimeWorkingHours = 1,
        [Description("Day/Block release")]
        DayBlockRelease = 2,
        [Description("Evening")]
        Evening = 3,
        [Description("Twilight")]
        Twilight = 4,
        [Description("Weekend")]
        Weekend = 5,
        [Description("Customised")]
        Customised = 6,
        [Description("Not known")]
        NotKnown = 7,
        [Description("Not applicable")]
        NotApplicable = 8
    }


    public class TribalCourseRun : ITribalCourseRun
    {
        //public Guid id { get; set; }
        public int CourseId { get; set; }
        public int CourseInstanceId { get; set; }
        public int? VenueId { get; set; } //=>  Call VenueService to get [VenueId](GUID) using [VenueLocationId] => TODO
        // public string CourseName { get; set; } => it will use CourseTitle from Course properties
        public Guid? VenueGuidId { get; set; }
        public string CourseName { get; set; }
        public string ProviderOwnCourseInstanceRef { get; set; } //=> ProviderCourseId ??? [ProviderOwnCourseInstanceRef] instead of CourseInstanceId
        public AttendanceType AttendanceType { get; set; } //=> DeliveryMode DeliveryMode
        public string StartDateDescription { get; set; } //=> FlexibleStartDate
        // Flexible start date - please just contact the programme to sign up
        // Flexible start dates throughout the year to suit the client / individual 
        // 5488 distinct values (358 of which contain the word 'Flexible')
        // => public bool FlexibleStartDate { get; set; }  // The course starts on 19/9/18
        public DateTime? StartDate { get; set; } //=> StartDate
        public string Url { get; set; } // => CourseURL
        public decimal? Price { get; set; } // => Cost
        public string PriceAsText { get; set; } //=> CostDescription
        public TribalDurationUnit DurationUnit { get; set; }
        public int? DurationValue { get; set; }
        public TribalStudyMode StudyMode { get; set; }
        public TribalAttendancePattern AttendancePattern { get; set; }

        public RecordStatus RecordStatus { get; set; }
        public TribalRecordStatus TribalRecordStatus { get; set; }
        public string VenueName { get; set; } //=> CostDescription
        //public DateTime CreatedDate { get; set; }
        //public string CreatedBy { get; set; }
        //public DateTime UpdatedDate { get; set; }
        //public string UpdatedBy { get; set; }
    }
}
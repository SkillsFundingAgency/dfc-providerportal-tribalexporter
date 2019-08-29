using Dfc.CourseDirectory.Models.Interfaces.Courses;
using System;
using System.Collections.Generic;

namespace Dfc.CourseDirectory.Models.Models.Courses
{
    public class TribalCourse : ITribalCourse 
    {
        public int CourseId { get; set; } // Used to get TribalCourseRuns
        public string CourseTitle  { get; set; } // QualificationCourseTitle
        public string LearningAimRefId  { get; set; } // LearnAimRef => LARS => check 54007 = NULL empty ??? (Cleansing for duplicate course - for location
        public int QualificationLevelId { get; set; } // NotionalNVQLevelv2
        public string QualificationLevelIdString { get; set; }
        public string LearningAimAwardOrgCode { get; set; } // AwardOrgCode
        public string Qualification { get; set; } // QualificationType => ??? many of them empty NULL shall we add "Other"

        public int Ukprn { get; set; } // ProviderUKPRN

        public string CourseSummary { get; set; } // CourseDescription
        public string EntryRequirements { get; set; } // EntryRequirments
        public string WhatYoullLearn { get; set; } // ??? TBC
        public string HowYoullLearn { get; set; } // ??? TBC
        public string EquipmentRequired  { get; set; } // ???  WhatYoullNeed
        public string AssessmentMethod  { get; set; } // HowYoullBeAssessed
        public string WhereNext { get; set; } // ???  TBC 

        public bool AdvancedLearnerLoan { get; set; } // ??? NOT done
      
        public IEnumerable<TribalCourseRun> TribalCourseRuns { get; set; }
    }
}
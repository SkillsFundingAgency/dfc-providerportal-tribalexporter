using System;
using System.Collections.Generic;
using Dfc.CourseDirectory.Models.Models.Courses;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    class MappingHelper
    {
        public static Course MapTribalCourseToCourse(TribalCourse tribalCourse, int numberOfMonthsAgo, bool dummyMode, out List<string> mappingMessages, out bool courseNOTtoBeMigrated)
        {
            var course = new Course();
            var courseRuns = new List<CourseRun>();
            mappingMessages = new List<string>();
            courseNOTtoBeMigrated = false;
            //var courseRunsToBeRemovedAsTooOld = new List<CourseRun>();

            foreach (var tribalCourseRun in tribalCourse.TribalCourseRuns)
            {
                var courseRun = new CourseRun();

                // JUST FOR TESTING - DO NOT UNCOMMENT
                //tribalCourseRun.AttendanceType = AttendanceType.DistanceWithAttendance;
                //tribalCourseRun.DurationUnit = TribalDurationUnit.Terms;
                //tribalCourseRun.StudyMode = TribalStudyMode.PartOfAFulltimeProgram;
                //tribalCourseRun.AttendancePattern = TribalAttendancePattern.Customised;

                courseRun.id = Guid.NewGuid();
                courseRun.CourseInstanceId = tribalCourseRun.CourseInstanceId;
                courseRun.VenueId = tribalCourseRun.VenueGuidId;
                courseRun.CourseName = tribalCourseRun.CourseName;
                courseRun.ProviderCourseID = tribalCourseRun.ProviderOwnCourseInstanceRef;

                // AttendanceType <=> DeliveryMode,
                switch (tribalCourseRun.AttendanceType)
                {
                    case AttendanceType.Location:
                        courseRun.DeliveryMode = DeliveryMode.ClassroomBased;
                        break;
                    case AttendanceType.WorkBased:
                    case AttendanceType.FaceToFaceNonCampus:
                        if (null != courseRun.VenueId)
                            courseRun.DeliveryMode = DeliveryMode.ClassroomBased;
                        else
                            courseRun.DeliveryMode = DeliveryMode.WorkBased;
                        break;
                    case AttendanceType.OnlineWithoutAttendance:
                    case AttendanceType.OnlineWithAttendance:
                        courseRun.DeliveryMode = DeliveryMode.Online;
                        break;
                    case AttendanceType.MixedMode:
                    case AttendanceType.DistanceWithAttendance:
                    case AttendanceType.DistanceWithoutAttendance:
                    case AttendanceType.NotKnown:
                    case AttendanceType.Undefined:
                    default:
                        courseRun.DeliveryMode = DeliveryMode.Undefined;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING " +
                                            $"because your AttendanceType is set to { tribalCourseRun.AttendanceType } and we don't have it" + Environment.NewLine);
                        break;
                }

                // StartDate & FlexibleStartDate
                // Uncomment for testing only
                //tribalCourseRun.StartDate = DateTime.Now.AddMonths(-5);
                if (tribalCourseRun.StartDate != null && tribalCourseRun.StartDate > DateTime.MinValue)
                {
                    courseRun.StartDate = tribalCourseRun.StartDate;
                    courseRun.FlexibleStartDate = false;

                    //if (tribalCourseRun.StartDate >= (DateTime.Now.AddMonths(-numberOfMonthsAgo)))
                    //{
                    //    courseRun.StartDate = tribalCourseRun.StartDate;
                    //    courseRun.FlexibleStartDate = false;
                    //}
                    //else
                    //{
                    //    //courseRunsIsTooOld.Add(true);
                    //    courseRunsToBeRemovedAsTooOld.Add(courseRun);
                    //    mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' was REMOVED " +
                    //                        $"because the CourseRun StartDate ( { tribalCourseRun.StartDate.Value.ToShortDateString() } ) was more than 3 months ago and we didn't migrate it" + Environment.NewLine);
                    //}
                }
                else
                {
                    // latest decision Imran & Mark C. 
                    courseRun.StartDate = null;
                    courseRun.FlexibleStartDate = false;
                    mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' was set to Pending, because it didn't have StartDate " + Environment.NewLine);
                }

                courseRun.CourseURL = tribalCourseRun.Url;
                courseRun.Cost = tribalCourseRun.Price;
                courseRun.CostDescription = tribalCourseRun.PriceAsText;

                // DurationUnit & DurationValue
                switch (tribalCourseRun.DurationUnit)
                {
                    case TribalDurationUnit.Hours:
                        courseRun.DurationValue = tribalCourseRun.DurationValue;
                        courseRun.DurationUnit = DurationUnit.Hours;
                        break;
                    case TribalDurationUnit.Days:
                        courseRun.DurationValue = tribalCourseRun.DurationValue;
                        courseRun.DurationUnit = DurationUnit.Days;
                        break;
                    case TribalDurationUnit.Weeks:
                        courseRun.DurationValue = tribalCourseRun.DurationValue;
                        courseRun.DurationUnit = DurationUnit.Weeks;
                        break;
                    case TribalDurationUnit.Months:
                        courseRun.DurationValue = tribalCourseRun.DurationValue;
                        courseRun.DurationUnit = DurationUnit.Months;
                        break;
                    case TribalDurationUnit.Terms:

                        if (tribalCourseRun.DurationValue == null)
                        {
                            courseRun.DurationValue = null;
                        }
                        else
                        {
                            courseRun.DurationValue = (tribalCourseRun.DurationValue ?? 0) * 3;
                        }
                        courseRun.DurationUnit = DurationUnit.Months;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' was set to DurationUnit = { tribalCourseRun.DurationUnit } " +
                                            $"and  DurationValue = { tribalCourseRun.DurationValue }. We needed to convert it to  DurationUnit = { courseRun.DurationUnit } and DurationValue = { courseRun.DurationValue }." + Environment.NewLine);
                        //courseRun.DurationValue = tribalCourseRun.DurationValue;
                        //courseRun.DurationUnit = DurationUnit.Undefined;
                        //// Alternativly 3 x Months or X x Weeks // TODO 
                        //courseRun.RecordStatus = RecordStatus.MigrationPending;
                        //mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING " +
                        //                    $"because your DurationUnit is set to { tribalCourseRun.DurationUnit } and we don't have it" +
                        //                    $"We preserved the DurationValue, but you have to set appropriate DurationUnit and change the DurationValue accordingly" + Environment.NewLine);
                        break;
                    case TribalDurationUnit.Semesters:
                        courseRun.DurationValue = null;  // New requirement - null
                        courseRun.DurationUnit = DurationUnit.Undefined;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' was set to DurationUnit = Semesters " +
                            $"and  DurationValue = { tribalCourseRun.DurationValue }. 'Semester' DurationUnit is not supported." + Environment.NewLine);
                        // $"and  DurationValue = { tribalCourseRun.DurationValue }. We preserved the DurationValue = { courseRun.DurationValue }, but you need to select available DurationUnit and change the DurationValue accordingly." + Environment.NewLine);
                        //$"and  DurationValue = { tribalCourseRun.DurationValue }. 'Semester' DurationUnit is not supported." + Environment.NewLine);
                        break;
                    /*
                                        case TribalDurationUnit.Semesters:
                                            if (tribalCourseRun.DurationValue == null)
                                                courseRun.DurationValue = null;
                                            else
                                                courseRun.DurationValue = (tribalCourseRun.DurationValue ?? 0) * 3;
                                            courseRun.DurationUnit = DurationUnit.Months;
                                            mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' was set to DurationUnit = { tribalCourseRun.DurationUnit } " +
                                                                $"and  DurationValue = { tribalCourseRun.DurationValue }. We needed to convert it to  DurationUnit = { courseRun.DurationUnit } and DurationValue = { courseRun.DurationValue }." + Environment.NewLine);


                                            //courseRun.DurationValue = tribalCourseRun.DurationValue;
                                            //courseRun.DurationUnit = DurationUnit.Undefined;
                                            //// Alternativly 3 x Months or X x Weeks // TODO 
                                            //courseRun.RecordStatus = RecordStatus.MigrationPending;
                                            //mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING " +
                                            //                    $"because your DurationUnit is set to { tribalCourseRun.DurationUnit } and we don't have it" +
                                            //                    $"We preserved the DurationValue, but you have to set appropriate DurationUnit and change the DurationValue accordingly" + Environment.NewLine);
                                            break;
                    */
                    case TribalDurationUnit.Years:
                        courseRun.DurationValue = tribalCourseRun.DurationValue;
                        courseRun.DurationUnit = DurationUnit.Years;
                        break;
                    case TribalDurationUnit.Undefined:
                    default:
                        courseRun.DurationUnit = DurationUnit.Undefined;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING " +
                                            $"because your DurationUnit is set to { tribalCourseRun.DurationUnit } and we don't have it" + Environment.NewLine);
                        break;
                }

                // StudyMode 
                switch (tribalCourseRun.StudyMode)
                {
                    case TribalStudyMode.FullTime:
                        courseRun.StudyMode = StudyMode.FullTime;
                        break;
                    case TribalStudyMode.PartTime:
                        courseRun.StudyMode = StudyMode.PartTime;
                        break;
                    case TribalStudyMode.Flexible:
                        courseRun.StudyMode = StudyMode.Flexible; // Here it looks identical but value changes from 4 to 3
                        break;
                    case TribalStudyMode.PartOfAFulltimeProgram:
                    case TribalStudyMode.NotKnown:
                    case TribalStudyMode.Undefined:
                    default:
                        courseRun.StudyMode = StudyMode.Undefined;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING " +
                                            $"because your StudyMode is set to { tribalCourseRun.StudyMode } and we don't have it" + Environment.NewLine);
                        break;
                }
                // AttendancePattern 
                switch (tribalCourseRun.AttendancePattern)
                {
                    case TribalAttendancePattern.DaytimeWorkingHours:
                        courseRun.AttendancePattern = AttendancePattern.Daytime;
                        break;
                    case TribalAttendancePattern.DayBlockRelease:
                        courseRun.AttendancePattern = AttendancePattern.DayOrBlockRelease; // Here it looks identical but value changes from 2 to 4
                        break;
                    case TribalAttendancePattern.Evening:
                    case TribalAttendancePattern.Twilight:
                        courseRun.AttendancePattern = AttendancePattern.Evening;
                        break;
                    case TribalAttendancePattern.Weekend:
                        courseRun.AttendancePattern = AttendancePattern.Weekend; // Here it looks identical but value changes from 5 to 3
                        break;
                    case TribalAttendancePattern.Customised:
                    case TribalAttendancePattern.NotKnown:
                    case TribalAttendancePattern.NotApplicable:
                    case TribalAttendancePattern.Undefined:
                    default:
                        courseRun.AttendancePattern = AttendancePattern.Undefined;
                        mappingMessages.Add($"ATTENTION - CourseRun { tribalCourseRun.CourseInstanceId } with Ref: '{ tribalCourseRun.ProviderOwnCourseInstanceRef }' is set to PENDING, " +
                                            $"because your AttendancePattern is set to { tribalCourseRun.AttendancePattern } and we don't have it" + Environment.NewLine);
                        break;
                }

                courseRun.CreatedDate = DateTime.Now;
                courseRun.CreatedBy = "DFC – Course Migration Tool";

                courseRuns.Add(courseRun);
            }

            course.id = Guid.NewGuid();
            course.CourseId = tribalCourse.CourseId;
            course.QualificationCourseTitle = tribalCourse.CourseTitle;
            course.LearnAimRef = tribalCourse.LearningAimRefId;
            course.NotionalNVQLevelv2 = tribalCourse.QualificationLevelIdString;
            course.AwardOrgCode = tribalCourse.LearningAimAwardOrgCode;
            course.QualificationType = tribalCourse.Qualification;
            course.ProviderUKPRN = tribalCourse.Ukprn;
            course.CourseDescription = tribalCourse.CourseSummary;
            course.EntryRequirements = tribalCourse.EntryRequirements;
            course.WhatYoullLearn = tribalCourse.WhatYoullLearn;
            course.HowYoullLearn = tribalCourse.HowYoullLearn;
            course.WhatYoullNeed = tribalCourse.EquipmentRequired;
            course.HowYoullBeAssessed = tribalCourse.AssessmentMethod;
            course.WhereNext = tribalCourse.WhereNext;
            course.AdvancedLearnerLoan = tribalCourse.AdvancedLearnerLoan;
            course.AdultEducationBudget = false; // WE don't have the data/or rule for it.
            course.CreatedDate = DateTime.Now;
            course.CreatedBy = "DFC – Course Migration Tool";
            // Removing CourseRuns, which are older than 3 (configurable) months
            //foreach(var courseRunToBeRemovedAsTooOld in courseRunsToBeRemovedAsTooOld)
            //{
            //    courseRuns.Remove(courseRunToBeRemovedAsTooOld);
            //}

            if (courseRuns != null && courseRuns.Count > 0)
            {
                course.CourseRuns = courseRuns;
            }
            else
            {
                // We don't migrate courses without CourseRuns
                courseNOTtoBeMigrated = true;
            }

            return course;
        }
    }
}

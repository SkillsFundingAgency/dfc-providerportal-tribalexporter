
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Common.Interfaces;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Interfaces.Courses;
using Dfc.CourseDirectory.Models.Models.Courses;


namespace Dfc.CourseDirectory.Services.Interfaces.CourseService
{
    public interface ICourseService
    {
        Task<IResult<ICourse>> AddCourseAsync(ICourse course);
        Task<IResult<List<string>>> DeleteCoursesByUKPRNAsync(IDeleteCoursesByUKPRNCriteria criteria);
        IList<string> ValidateCourse(ICourse course);
        IList<string> ValidateCourseRun(ICourseRun courseRun, ValidationMode validationMode);
        Task<IResult> AddMigrationReport(CourseMigrationReport courseMigrationReport);
    }
}

using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseCollectionService
    {
        Task<List<Course>> GetAllCoursesAsync();
        Task<List<Course>> GetAllCoursesByUkprnAsync(int ukprn);
        Task<string> GetAllLiveCoursesAsJsonForUkprnAsync(int ukprn);
        Task<bool> HasCoursesBeenCreatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenCreatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCoursesBeenUpdatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenUpdatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCoursesBeenDeletedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenDeletedSinceAsync(int ukprn, DateTime date);
    }
}
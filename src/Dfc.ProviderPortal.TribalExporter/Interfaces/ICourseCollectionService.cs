using System;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseCollectionService
    {
        Task<string> GetAllLiveCoursesAsJsonForUkprnAsync(int ukprn);
        Task<bool> HasCoursesBeenCreatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenCreatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCoursesBeenUpdatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenUpdatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCoursesBeenDeletedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenDeletedSinceAsync(int ukprn, DateTime date);
    }
}
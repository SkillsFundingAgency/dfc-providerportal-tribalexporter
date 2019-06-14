using System;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseService
    {
        Task<string> GetAllLiveCoursesAsJsonForUkprnAsync(int ukprn);
        Task<bool> HasCoursesBeenUpdatedSinceAsync(int ukprn, DateTime date);
        Task<bool> HasCourseRunsBeenUpdatedSinceAsync(int ukprn, DateTime date);
    }
}
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseService
    {
        Task<string> GetAllLiveCoursesAsJsonForUkprnAsync(int ukprn);
    }
}
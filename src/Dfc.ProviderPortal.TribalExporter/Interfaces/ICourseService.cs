using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseService
    {
        Task<string> GetAllCoursesAsJsonForUkprnAsync(int ukprn);
    }
}
using Dfc.CourseDirectory.Models.Models.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IFEChoicesDataCollectionService
    {
        Task<FEChoicesData> GetDocumentByUkprn(int ukprn);
        Task<List<FEChoicesData>> GetAllDocument();
    }
}
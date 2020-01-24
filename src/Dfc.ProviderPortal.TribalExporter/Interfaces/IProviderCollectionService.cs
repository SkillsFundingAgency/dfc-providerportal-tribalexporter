using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IProviderCollectionService
    {
        Task<string> GetAllAsJsonAsync(IEnumerable<int> ukprns);
        Task<bool> ProviderExists(int ukprn);
    }
}
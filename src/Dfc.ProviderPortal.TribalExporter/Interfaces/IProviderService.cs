using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IProviderService
    {
        Task<string> GetAllAsJsonAsync(IEnumerable<int> ukprns);
    }
}
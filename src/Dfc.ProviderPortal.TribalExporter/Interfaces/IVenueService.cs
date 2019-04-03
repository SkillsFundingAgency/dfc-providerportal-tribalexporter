using System;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IVenueService
    {
        Task<string> GetAllVenuesAsJsonForUkprnAndDateAsync(int ukprn, DateTime date);
    }
}
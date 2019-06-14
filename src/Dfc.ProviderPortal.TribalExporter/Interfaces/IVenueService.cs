using System;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IVenueService
    {
        Task<string> GetAllVenuesAsJsonForUkprnAsync(int ukprn);
        Task<bool> HasBeenAnUpdatedSinceAsync(int ukprn, DateTime date);
    }
}
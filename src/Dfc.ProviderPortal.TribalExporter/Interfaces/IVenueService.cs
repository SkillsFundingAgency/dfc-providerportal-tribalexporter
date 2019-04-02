using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IVenueService
    {
        IEnumerable<IVenue> GetVenuesByProvider(IProvider provider);
    }
}
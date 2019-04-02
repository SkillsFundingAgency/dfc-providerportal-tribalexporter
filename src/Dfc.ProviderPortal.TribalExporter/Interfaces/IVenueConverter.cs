using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IVenueConverter
    {
        ITribalVenue Convert(IVenue venue);

        IEnumerable<ITribalVenue> Convert(IEnumerable<IVenue> venues);
    }
}
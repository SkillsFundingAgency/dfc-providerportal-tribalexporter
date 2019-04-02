using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models;
using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Converters
{
    public class VenueConverter : IVenueConverter
    {
        public ITribalVenue Convert(IVenue venue)
        {
            Throw.IfNull(venue, nameof(venue));

            // mapping happens here ...

            return new TribalVenue();
        }

        public IEnumerable<ITribalVenue> Convert(IEnumerable<IVenue> venues)
        {
            Throw.IfNullOrEmpty(venues, nameof(venues));

            var list = new List<ITribalVenue>();
            foreach (var venue in venues) list.Add(Convert(venue));

            return list;
        }
    }
}
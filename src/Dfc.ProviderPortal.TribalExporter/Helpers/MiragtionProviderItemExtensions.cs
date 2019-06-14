using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class MiragtionProviderItemExtensions
    {
        public static IEnumerable<int> AsUkprns(this IEnumerable<IMiragtionProviderItem> items)
        {
            if (items == null || !items.Any()) return new List<int>();
            return items.Select(x => x.Ukprn);
        }
    }
}
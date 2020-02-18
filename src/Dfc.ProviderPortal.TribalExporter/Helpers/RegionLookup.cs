using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dfc.ProviderPortal.TribalExporter.Models;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class RegionLookup
    {
        private static readonly ISet<int> s_nationalVenueLocations = new HashSet<int>(new[]
        {
            1,  // ENGLAND
            100289,  // WORLD
            23241,  // NON-UK
            21034  // 21034
        });
        private static Dictionary<int, string> s_lookup;

        public static IReadOnlyCollection<string> FindRegions(int venueLocationId)
        {
            var lookup = LazyInitializer.EnsureInitialized(ref s_lookup, LoadLookupFile);

            lookup.TryGetValue(venueLocationId, out var regionId);
            
            if (regionId == default)
            {
                return Array.Empty<string>();
            }
            else
            {
                // If result is a region (not a sub-region) we need to expand to all the sub regions
                var region = RegionInfo.All.SingleOrDefault(r => r.Id == regionId);
                if (region != null)
                {
                    return region.SubRegions.Select(sr => sr.Id).ToList();
                }
                else
                {
                    return new[] { regionId };
                }
            }

            Dictionary<int, string> LoadLookupFile()
            {
                var results = new Dictionary<int, string>();

                // Keep a record of all the VenueLocationIds so we can detect duplicates.
                // Don't add duplicates to the output - it should be a lookup error.
                var venueLocationIds = new HashSet<int>();

                var lookupFileResourceName = "Dfc.ProviderPortal.TribalExporter.VenueLookup.txt";
                using (var lookupFile = typeof(RegionLookup).Assembly.GetManifestResourceStream(lookupFileResourceName))
                using (var reader = new StreamReader(lookupFile))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var parts = line.Split("\t,".ToCharArray());
                        var vlId = int.Parse(parts[0]);
                        var region = parts[1];

                        if (venueLocationIds.Add(vlId))
                        {
                            results.Add(vlId, region);
                        }
                        else
                        {
                            // Duplicate - remove all for VenueLocationId
                            results.Remove(vlId);
                        }
                    }
                }

                return results;
            }
        }

        public static bool IsNational(int venueLocationId) => s_nationalVenueLocations.Contains(venueLocationId);
    }
}

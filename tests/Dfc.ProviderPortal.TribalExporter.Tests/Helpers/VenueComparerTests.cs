using Dfc.CourseDirectory.Models.Models.Venues;
using System;
using System.Collections.Generic;
using Xunit;
using System.Linq;
using Dfc.CourseDirectory.Models.Helpers;

namespace Dfc.ProviderPortal.TribalExporter.Tests.Helpers
{

    public class VenueComparerTests
    {
        //id is different, but same address (first line, postcode).
        //Address is different
        //address is the same (firstline)
        //provider is different

        //1 distinct venue, no duplicate
        //2 distinct venues, 1 duplicate
        //3 distinct venues, 2 duplicates
        //id is the same, group by one


        //VenueName can be unique
        //Postcode
        //Address1

        [Fact]
        public void groupby_two_distinct_addresses()
        {
            var lst = new List<Venue>() { 
            new Venue()
            {
                ID = Guid.Empty.ToString(),
                Address1 = "address1",
                VenueName = "test",
                PostCode = "test"
            }, 
            new Venue()
            {
                ID = Guid.Empty.ToString(),
                Address1 = "test",
                VenueName = "test",
                PostCode = "test"
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "test",
                VenueName = "test",
                PostCode = "test"
            }};

            var comp = new VenueEqualityComparer();
            var uniqueGroups = lst.GroupBy(x => x,  comp);
            Assert.True(uniqueGroups.Count() == 2, $"Expected 2 Groups but got {uniqueGroups.Count()}");
        }
    }
}

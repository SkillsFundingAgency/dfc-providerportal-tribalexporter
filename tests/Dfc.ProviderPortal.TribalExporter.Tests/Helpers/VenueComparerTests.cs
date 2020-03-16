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
        [Fact]
        public void can_group_by_single_venue()
        {
            var lst = new List<Venue>() {
            new Venue()
            {
                ID = Guid.Empty.ToString(),
                Address1 = "address1",
                VenueName = "venue1",
                PostCode = "postcode1"
            } };

            var comp = new VenueEqualityComparer();
            var uniqueGroups = lst.GroupBy(x => x, comp);
            Assert.True(uniqueGroups.Count() == 1, $"Expected 1 Groups but got {uniqueGroups.Count()}");
        }

        [Fact]
        public void that_there_are_no_duplicates_with_distinct_venues()
        {
            var lst = new List<Venue>() {
            new Venue()
            {
                ID = Guid.Empty.ToString(),
                Address1 = "address1",
                VenueName = "venue1",
                PostCode = "postcode1"
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address2",
                VenueName = "venue2",
                PostCode = "postcode2"
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address3",
                VenueName = "venue3",
                PostCode = "postcode3"
            }};

            var comp = new VenueEqualityComparer();
            var uniqueGroups = lst.GroupBy(x => x, comp);
            Assert.True(uniqueGroups.Count() == 3, $"Expected 3 Groups but got {uniqueGroups.Count()}");
        }

        [Fact]
        public void that_can_groupby_address_and_ukprn()
        {

            //lst1 - duplicate adddresses different ukprn are grouped
            var lstDifferentUKPRN = new List<Venue>() {
            new Venue()
            {
                 ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=1
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=2
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=3
            }};

            //lst2 - duplicate adddresses for same ukprn are grouped
            var lst2SameUKPRN = new List<Venue>() {
            new Venue()
            {
                 ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=1
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=1
            },
            new Venue()
            {
                ID = Guid.NewGuid().ToString(),
                Address1 = "address1",
                VenueName = "address1",
                PostCode = "postcode1",
                UKPRN=3
            }};

            var comp = new VenueEqualityComparer();
            var uniqueGroupsDifferentUKPRN = lstDifferentUKPRN.GroupBy(x => x, comp);
            var uniqueGroupsSameUKPRN = lst2SameUKPRN.GroupBy(x => x, comp);
            Assert.True(uniqueGroupsDifferentUKPRN.Count() == 3, $"Expected 3 Groups but got {uniqueGroupsDifferentUKPRN.Count()}");
            Assert.True(uniqueGroupsSameUKPRN.Count() == 2, $"Expected 2 Groups but got {uniqueGroupsSameUKPRN.Count()}");
        }

        [Fact]
        public void that_there_are_two_distinct_addresses()
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
            var uniqueGroups = lst.GroupBy(x => x, comp);
            Assert.True(uniqueGroups.Count() == 2, $"Expected 2 Groups but got {uniqueGroups.Count()}");
        }
    }
}

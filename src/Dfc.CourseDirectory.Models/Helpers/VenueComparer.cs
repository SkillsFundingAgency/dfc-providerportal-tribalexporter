using Dfc.CourseDirectory.Models.Models.Venues;
using System;
using System.Collections.Generic;

namespace Dfc.CourseDirectory.Models.Helpers
{
    public class VenueEqualityComparer : IEqualityComparer<Venue>
    {
        public bool Equals(Venue x, Venue y)
        {
            if ((x.VenueName?.ToLower() == y.VenueName?.ToLower() &&
                (x.PostCode?.ToLower() == y.PostCode?.ToLower()) &&
                (x.Address1?.ToLower() == y.Address1?.ToLower())) &&
                (x.UKPRN == y.UKPRN))
                return true;

            //not the same
            return false;
        }

        public int GetHashCode(Venue obj)
        {
            return HashCode.Combine(obj.VenueName?.ToLower(), obj.UKPRN, obj.Address1?.ToLower(), obj.PostCode?.ToLower());
        }
    }
}

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
                (x.Address1?.ToLower() == y.Address1?.ToLower()) &&
                (x.ProviderID == y.ProviderID)) ||
                (x.ID == y.ID)) 
                    return true;

            //not the same
            return false;
        }

        public int GetHashCode(Venue obj)
        {
            return HashCode.Combine(obj.VenueName, obj.ID, obj.Address1, obj.PostCode, obj.ProviderID);
        }
    }
}

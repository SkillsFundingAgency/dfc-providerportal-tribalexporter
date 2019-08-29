using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Services.VenueService
{
    public class GetVenueByVenueIdCriteria : ValueObject<GetVenueByVenueIdCriteria>, IGetVenueByVenueIdCriteria
    {
        public int venueId { get; set; }

        public GetVenueByVenueIdCriteria(int venueid)
        {
            Throw.IfNull(venueid, nameof(venueid));

            venueId = venueid;
        }
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return venueId;
        }
    }
}

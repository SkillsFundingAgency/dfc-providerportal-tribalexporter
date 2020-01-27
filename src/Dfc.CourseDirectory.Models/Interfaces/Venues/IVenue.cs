using System;
using System.Collections.Generic;
using System.Text;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Venues;

namespace Dfc.CourseDirectory.Models.Interfaces.Venues
{
    public interface IVenue
    {
        int UKPRN { get; set; }
        int ProviderID { get; }
        int VenueID { get; }
        string VenueName { get; }
        string ProvVenueID { get; }
        string Address1 { get; }
        string Address2 { get; }
        string Town { get; }
        string County { get; }
        string PostCode { get; }
        double? Latitude { get; set; }
        double? Longitude { get; set; }
        VenueStatus Status { get; set; }
        DateTime DateUpdated { get; }
        string UpdatedBy { get; }

        // Apprenticeship related
        long? LocationId { get; set; }
        string Telephone { get; set; }
        string Email { get; set; }
        string Website { get; set; }
        string ID { get; set; }
    }
}



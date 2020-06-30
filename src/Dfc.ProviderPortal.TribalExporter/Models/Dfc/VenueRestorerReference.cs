using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Dfc
{
    public class VenueRestorerReference
    {
        public Guid? ApprenticeshipId { get; set; }
        public Guid? CourseRunId { get; set; }
        public Guid? CourseId { get; set; }
        public int? ApprenticeshipLocationUKPRN { get; set; }
        public int UKPRN { get; set; }
        public string VenueId { get; set; }
        public int CurrentVenueUKPRN { get; set; }
        public int? RestoredVenueUKPRN { get; set; }
        public string CurrentVenueName { get; set; }
        public string CurrentAddress1 { get; set; }
        public string CurrentPostcode { get; set; }
        public string RestoredVenueName { get; set; }
        public string RestoredAddress1 { get; set; }
        public string RestoredPostcode { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public bool UKPRNMatched { get; set; }
    }
}

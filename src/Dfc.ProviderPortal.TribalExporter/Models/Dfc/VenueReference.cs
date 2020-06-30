using System;

namespace Dfc.ProviderPortal.TribalExporter.Models.Dfc
{
    public class VenueReference
    {
        public Guid? ApprenticeshipId { get; set; }
        public Guid? CourseRunId { get; set; }
        public Guid? CourseId { get; set; }
        public int? ApprenticeshipLocationUKPRN { get; set; }
        public int UKPRN { get; set; }
        public string VenueId { get; set; }
        public int VenueUKPRN { get; set; }
        public string VenueName { get; set; }
        public string Address1 { get; set; }
        public string Postcode { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public bool UKPRNMatched { get; set; }
    }
}

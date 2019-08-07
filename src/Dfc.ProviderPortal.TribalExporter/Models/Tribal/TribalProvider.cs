using Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class TribalProvider : ITribalProvider
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public double? EmployerSatisfaction { get; set; }
        public List<Framework> Frameworks { get; set; }
        public double? LearnerSatisfaction { get; set; }
        public List<Location> Locations { get; set; }
        public string MarketingInfo { get; set; }
        public string Name { get; set; }
        public bool NationalProvider { get; set; }
        public string Phone { get; set; }
        public List<Standard> Standards { get; set; }
        public int UKPRN { get; set; }
        public string Website { get; set; }

    }
}

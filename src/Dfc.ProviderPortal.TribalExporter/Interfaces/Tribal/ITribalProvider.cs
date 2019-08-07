using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface ITribalProvider
    {
        int Id { get; set; }
        string Email { get; set; }
        double? EmployerSatisfaction { get; set; }
        List<Framework> Frameworks { get; set; }
        double? LearnerSatisfaction { get; set; }
        List<Location> Locations { get; set; }
        string MarketingInfo { get; set; }
        string Name { get; set; }
        bool NationalProvider { get; set; }
        string Phone { get; set; }
        List<Standard> Standards { get; set; }
        int UKPRN { get; set; }
        string Website { get; set; }
    }
}

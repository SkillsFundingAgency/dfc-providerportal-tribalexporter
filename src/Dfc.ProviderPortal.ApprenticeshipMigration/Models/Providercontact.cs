using System;
using Dfc.CourseDirectory.Models.Interfaces.Providers;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Models
{
    public class Providercontact : IProvidercontact
    {
        public string ContactType { get; set; }
        public IContactaddress ContactAddress { get; set; }
        public IContactpersonaldetails ContactPersonalDetails { get; set; }
        public object ContactRole { get; set; }
        public string ContactTelephone1 { get; set; }
        public object ContactTelephone2 { get; set; }
        public string ContactFax { get; set; }
        public string ContactWebsiteAddress { get; set; }
        public string ContactEmail { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}

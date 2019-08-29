using Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Models
{
    public class ONSPDRegionSubregion : IONSPDRegionSubregion
    {
        public string Postcode { get; set; }
        public string Region { get; set; }
        public string SubRegion { get; set; }
    }
}

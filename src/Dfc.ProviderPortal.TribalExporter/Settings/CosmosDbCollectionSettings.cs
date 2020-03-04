using Dfc.ProviderPortal.TribalExporter.Interfaces;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class CosmosDbCollectionSettings : ICosmosDbCollectionSettings
    {
        public string CoursesCollectionId { get; set; }
        public string ProvidersCollectionId { get; set; }
        public string VenuesCollectionId { get; set; }
        public string ApprenticeshipCollectionId { get; set; }
        public string MigrationReportCoursesCollectionId { get; set; }
        public string MigrationReportApprenticeshipCollectionId { get; set; }
    }
}
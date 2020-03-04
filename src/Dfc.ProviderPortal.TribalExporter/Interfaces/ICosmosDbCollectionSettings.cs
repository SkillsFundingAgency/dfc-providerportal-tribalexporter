namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICosmosDbCollectionSettings
    {
        string CoursesCollectionId { get; }
        string ProvidersCollectionId { get; }
        string VenuesCollectionId { get; }
        string ApprenticeshipCollectionId { get; }
        string MigrationReportCoursesCollectionId { get; set; }
        string MigrationReportApprenticeshipCollectionId { get; set; }
    }
}
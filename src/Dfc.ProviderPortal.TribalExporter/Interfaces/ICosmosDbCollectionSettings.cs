namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICosmosDbCollectionSettings
    {
        string CoursesCollectionId { get; }
        string ProvidersCollectionId { get; }
        string VenuesCollectionId { get; }
    }
}
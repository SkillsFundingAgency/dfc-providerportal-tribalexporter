
namespace Dfc.CourseDirectory.Services.Interfaces.BlobStorageService
{
    public interface IBlobStorageSettings
    {
        string AccountName { get; }
        string AccountKey { get; }
        string Container { get; }
        string ConnectionString { get; }
        string TemplatePath { get; }
        string ProviderListPath { get; }

    }
}

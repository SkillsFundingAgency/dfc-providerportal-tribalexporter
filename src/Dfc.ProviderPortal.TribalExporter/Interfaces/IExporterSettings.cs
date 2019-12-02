using System;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IExporterSettings
    {
        DateTime ExporterStartDate { get; }
        DateTime ExporterEndDate { get; }
        string ContainerNameExporter { get; }
        string ContainerNameProviderFiles { get; }
        string MigrationProviderCsv { get; }
    }
}
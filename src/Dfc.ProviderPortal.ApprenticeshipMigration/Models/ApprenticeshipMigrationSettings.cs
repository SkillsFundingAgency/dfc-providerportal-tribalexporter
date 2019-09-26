using Dfc.CourseDirectory.Models.Enums;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Models
{
    public class ApprenticeshipMigrationSettings
    {
        public string ConnectionString { get; set; }
        public bool GenerateJsonFilesLocally { get; set; }
        public bool GenerateReportFilesLocally { get; set; }
        public string JsonApprenticeshipFilesPath { get; set; }
        public DeploymentEnvironment DeploymentEnvironment { get; set; }
        public bool DeleteCoursesByUKPRN { get; set; }
        public bool UpdateProvider { get; set; }
        public int VenueBasedRadius { get; set; }
        public int RegionBasedRadius { get; set; }
        public int SubRegionBasedRadius { get; set; }
        public int RegionSubRegionRangeRadius { get; set; }
        public int MigrationWindow { get; set; }
    }
}

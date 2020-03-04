using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Models.Models.Reports
{
    public class MigrationReportEntry
    {
        public string id { get; set; }
        public string ProviderName { get; set; }
        public int ProviderType { get; set; }
        public DateTime? MigrationDate { get; set; }
        public int? MigratedCount { get; set; }
        public int? FailedMigrationCount { get; set; }
        public int LiveCount { get; set; }
        public int PendingCount { get; set; }
        public int BulkUploadPendingcount { get; set; }
        public int BulkUploadReadyToGoLiveCount { get; set; }
        public int MigrationPendingCount { get; set; }
        public int MigrationReadyToGoLive { get; set; }
        public decimal MigrationRate { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
    }
}

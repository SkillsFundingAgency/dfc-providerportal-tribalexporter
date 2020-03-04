using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Reports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IMigrationReportCollectionService
    {
        Task<MigrationReportEntry> GetReportForApprenticeshipByUkprn(int ukprn);
        Task<MigrationReportEntry> GetReportForCoursesByUkprn(int ukprn);
    }
}
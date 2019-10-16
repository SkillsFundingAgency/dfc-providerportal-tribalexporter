using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Common.Interfaces;
using Dfc.CourseDirectory.Models.Interfaces.Apprenticeships;
using Dfc.ProviderPortal.Apprenticeships.Models;

namespace Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService
{
    public interface IApprenticeshipService
    {
        Task<IResult<IApprenticeship>> AddApprenticeshipAsync(IApprenticeship apprenticeship);
        Task<IResult<List<string>>> DeleteApprenticeshipsByUKPRNAsync(int ukprn);

        Task<IResult> AddApprenticeshipMigrationReportAsync(ApprenticeshipMigrationReport report);
    }
}

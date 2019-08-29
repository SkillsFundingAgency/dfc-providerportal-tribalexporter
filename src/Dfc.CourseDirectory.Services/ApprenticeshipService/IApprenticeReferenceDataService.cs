using Dfc.CourseDirectory.Common.Interfaces;
using System.Threading.Tasks;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    public interface IApprenticeReferenceDataService
    {
        Task<IResult<IApprenticeshipFrameworkSearchResult>> GetFrameworkByCode(int code, int progType, int pathWayCode);
        Task<IResult<IApprenticeshipStandardSearchResult>> GetStandardById(int code, int version);
    }
}

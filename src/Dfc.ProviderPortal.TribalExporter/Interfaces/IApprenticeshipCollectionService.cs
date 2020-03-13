using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IApprenticeshipCollectionService
    {
        Task<List<Apprenticeship>> GetAllApprenticeshipsAsync();
        Task<List<Apprenticeship>> GetAllApprenticeshipsByUkprnAsync(string ukprnt);
    }
}
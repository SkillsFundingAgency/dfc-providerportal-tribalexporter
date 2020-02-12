using Dfc.CourseDirectory.Models.Interfaces.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IApprenticeshipServiceWrapper
    {
        string GetApprenticeshipDeltaUpdatesAsJson();
        Task<Apprenticeship> GetApprenticeshipByApprenticeshipID(int apprenticeshipId);
    }
}

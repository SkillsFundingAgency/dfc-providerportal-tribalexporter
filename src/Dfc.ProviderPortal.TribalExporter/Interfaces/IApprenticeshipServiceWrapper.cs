using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IApprenticeshipServiceWrapper
    {
        IEnumerable<TribalProvider> GetApprenticeshipDeltaUpdates();
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IApprenticeshipServiceSettings
    {
        string ApiUrl { get; }
        string ApiKey { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UkrlpService;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IUkrlpApiService
    {
        List<ProviderRecordStructure> GetAllProviders();
    }
}

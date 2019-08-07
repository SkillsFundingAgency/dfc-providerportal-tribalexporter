using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface IFramework
    {
        IContact Contact { get; set; }
        int FrameworkCode { get; set; }
        int? ProgType { get; set; }
        int? Level { get; set; }
        List<ILocationRef> Locations { get; set; }
        int? PathwayCode { get; set; }
        string FrameworkInfoUrl { get; set; }
        string MarketingInfo { get; set; }
    }
}

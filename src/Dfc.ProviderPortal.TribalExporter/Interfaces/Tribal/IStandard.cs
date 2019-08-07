using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface IStandard
    {
        IContact Contact { get; set; }
        List<ILocationRef> Locations { get; set; }
        string MarketingInfo { get; set; }
        int StandardCode { get; set; }
        string StandardInfoUrl { get; set; }
    }
}

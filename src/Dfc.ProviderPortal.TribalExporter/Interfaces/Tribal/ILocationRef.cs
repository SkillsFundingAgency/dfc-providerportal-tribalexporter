using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface ILocationRef
    {
        List<string> DeliveryModes { get; set; }
        int ID { get; set; }
        string MarketingInfo { get; set; }
        int Radius { get; set; }
        string StandardInfoUrl { get; set; }
    }
}

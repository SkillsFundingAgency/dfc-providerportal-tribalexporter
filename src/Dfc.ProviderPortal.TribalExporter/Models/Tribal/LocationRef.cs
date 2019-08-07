using Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class LocationRef : ILocationRef
    {
        public List<string> DeliveryModes { get; set; }
        public int ID { get; set; }
        public string MarketingInfo { get; set; }
        public int Radius { get; set; }
        public string StandardInfoUrl { get; set; }

    }
}

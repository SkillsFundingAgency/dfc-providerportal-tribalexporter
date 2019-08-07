using Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class Standard : IStandard
    {
        public IContact Contact { get; set; }
        public List<ILocationRef> Locations { get; set; }
        public string MarketingInfo { get; set; }
        public int StandardCode { get; set; }
        public string StandardInfoUrl { get; set; }
    }
}

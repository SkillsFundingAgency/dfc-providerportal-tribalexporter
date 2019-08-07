using Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class Framework : IFramework
    {
        public IContact Contact { get; set; }
        public int FrameworkCode { get; set; }
        public int? ProgType { get; set; }
        public int? Level { get; set; }
        public int GetProgType => (ProgType ?? Level) ?? 0;
        public List<ILocationRef> Locations { get; set; }
        public int? PathwayCode { get; set; }
        public string FrameworkInfoUrl { get; set; }
        public string MarketingInfo { get; set; }
    }
}

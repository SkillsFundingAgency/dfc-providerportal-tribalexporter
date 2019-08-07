using Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class Location : ILocation
    {
        public IAddress Address { get; set; }
        public int? ID { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Phone { get; set; }
    }
}

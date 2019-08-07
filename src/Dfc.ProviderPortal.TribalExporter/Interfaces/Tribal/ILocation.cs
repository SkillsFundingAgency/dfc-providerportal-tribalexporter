using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface ILocation
    {
        IAddress Address { get; set; }
        int? ID { get; set; }
        string Name { get; set; }
        string Email { get; set; }
        string Website { get; set; }
        string Phone { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces.Tribal
{
    public interface IContact
    {
        string ContactUsUrl { get; set; }
        string Email { get; set; }
        string Phone { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IMiragtionProviderItem
    {
        int Ukprn { get; }
        DateTime DateMigrated { get; }
    }
}

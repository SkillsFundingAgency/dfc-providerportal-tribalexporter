using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class MiragtionProviderItem : IMiragtionProviderItem
    {
        public int Ukprn { get; set; }
        public DateTime DateMigrated { get; set; }
    }
}

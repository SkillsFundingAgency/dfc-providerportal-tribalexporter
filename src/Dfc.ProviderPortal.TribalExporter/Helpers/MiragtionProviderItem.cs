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

        public override string ToString()
        {
            return $"{{ \"{nameof(Ukprn)}\": {Ukprn}, \"{nameof(DateMigrated)}\": \"{DateMigrated}\" }}";
        }
    }
}

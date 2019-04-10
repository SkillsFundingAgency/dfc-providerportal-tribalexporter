using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class BlobStorageSettings : IBlobStorageSettings
    {
        public string ConnectionString { get; set; }
    }
}

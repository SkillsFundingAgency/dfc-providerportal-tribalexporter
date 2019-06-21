using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IDelimitedFileSettings
    {
        char DelimitingCharacter { get; }
        bool IsFirstRowHeaders { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IDelimitedField
    {
        int Number { get; }
        string Value { get; }
        bool IsDoubleQuoted { get; }
    }
}

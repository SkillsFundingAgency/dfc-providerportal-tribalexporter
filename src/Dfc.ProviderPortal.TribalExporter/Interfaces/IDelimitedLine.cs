using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IDelimitedLine
    {
        int Number { get; }
        IReadOnlyList<IDelimitedField> Fields { get; }
    }
}

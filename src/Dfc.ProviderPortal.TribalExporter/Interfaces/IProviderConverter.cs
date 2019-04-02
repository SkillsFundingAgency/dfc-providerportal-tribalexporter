using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IProviderConverter
    {
        ITribalProvider Convert(IProvider provider);

        IEnumerable<ITribalProvider> Convert(IEnumerable<IProvider> providers);
    }
}
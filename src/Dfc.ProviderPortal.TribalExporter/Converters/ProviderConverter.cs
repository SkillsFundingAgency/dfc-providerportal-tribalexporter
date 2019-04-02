using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models;
using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Converters
{
    public class ProviderConverter : IProviderConverter
    {
        public ITribalProvider Convert(IProvider provider)
        {
            Throw.IfNull(provider, nameof(provider));

            // mapping happens here ...

            return new TribalProvider();
        }

        public IEnumerable<ITribalProvider> Convert(IEnumerable<IProvider> providers)
        {
            Throw.IfNullOrEmpty(providers, nameof(providers));

            var list = new List<ITribalProvider>();
            foreach (var provider in providers) list.Add(Convert(provider));

            return list;
        }
    }
}
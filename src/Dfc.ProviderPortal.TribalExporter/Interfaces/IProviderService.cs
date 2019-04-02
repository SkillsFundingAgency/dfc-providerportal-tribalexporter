using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IProviderService
    {
        IEnumerable<IProvider> GetAll();
    }
}
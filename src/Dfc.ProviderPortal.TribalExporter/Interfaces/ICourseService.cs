using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourseService
    {
        IEnumerable<ICourse> GetCourseForProvider(IProvider provider);
    }
}
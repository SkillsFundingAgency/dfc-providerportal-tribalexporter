using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICourse
    {
        IEnumerable<ICourseRun> CourseRuns { get; }
    }
}
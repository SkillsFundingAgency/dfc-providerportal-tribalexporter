using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System.Collections.Generic;

namespace Dfc.ProviderPortal.TribalExporter.Models
{
    public class Course : ICourse
    {
        public IEnumerable<ICourseRun> CourseRuns { get; set; }
    }
}
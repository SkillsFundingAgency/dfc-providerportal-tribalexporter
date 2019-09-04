using System.Collections.Generic;

namespace Dfc.CourseDirectory.Models.Models
{
    public class GetProviderUKPRNsFromBlobResult
    {
        public List<int> ProviderUKPRNs { get; set; } 
        public string errorMessageGetCourses { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Dfc
{
    public class GetProviderUKPRNsFromBlobResult
    {
        public List<int> ProviderUKPRNs { get; set; } 
        public string errorMessageGetCourses { get; set; }
    }
}

using Dfc.CourseDirectory.Models.Interfaces.Providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Models.Models.Providers
{
    public class FEChoicesData : IFEChoicesData
    {
        public Guid id { get; set; }
        public int UKPRN { get; set; }
        public double? LearnerSatisfaction { get; set; }
        public double? EmployerSatisfaction { get; set; }
        public DateTime CreatedDateTimeUtc { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string LastUpdatedBy { get; set; }
        public DateTime LastUpdatedOn { get; set; }
    }
}

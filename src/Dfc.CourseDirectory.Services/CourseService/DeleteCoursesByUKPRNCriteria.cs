using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Services.Interfaces.CourseService;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Services.CourseService
{
    public class DeleteCoursesByUKPRNCriteria : ValueObject<DeleteCoursesByUKPRNCriteria>, IDeleteCoursesByUKPRNCriteria
    {
        public int? UKPRN { get; set; }

        public DeleteCoursesByUKPRNCriteria(int? UKPRNvalue)
        {
            Throw.IfNull(UKPRNvalue, nameof(UKPRNvalue));
            UKPRN = UKPRNvalue;
        }
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return UKPRN;
        }
    }
}

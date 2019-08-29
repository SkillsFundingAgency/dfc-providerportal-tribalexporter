using System;
using System.Collections.Generic;
using System.Text;
using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    public class ApprenticeshipFrameworkSearchResult : ValueObject<ApprenticeshipFrameworkSearchResult>, IApprenticeshipFrameworkSearchResult
    {
        public ReferenceDataFramework Value { get; set; }

        public ApprenticeshipFrameworkSearchResult(
            ReferenceDataFramework value)
        {
            Throw.IfNull(value, nameof(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}

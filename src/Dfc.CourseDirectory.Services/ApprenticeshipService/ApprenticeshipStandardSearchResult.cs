using System;
using System.Collections.Generic;
using System.Text;
using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    class ApprenticeshipStandardSearchResult : ValueObject<ApprenticeshipStandardSearchResult>, IApprenticeshipStandardSearchResult
    {
        public ReferenceDateStandard Value { get; set; }

        public ApprenticeshipStandardSearchResult(
            ReferenceDateStandard value)
        {
            Throw.IfNull(value, nameof(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    
    }
}

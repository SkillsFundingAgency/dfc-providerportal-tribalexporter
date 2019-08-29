using System;
using System.Collections.Generic;
using System.Text;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    public interface IApprenticeshipFrameworkSearchResult
    {
        ReferenceDataFramework Value { get; set; }
    }
}

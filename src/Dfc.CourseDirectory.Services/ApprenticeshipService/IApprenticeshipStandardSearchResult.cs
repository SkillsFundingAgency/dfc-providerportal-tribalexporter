using System;
using System.Collections.Generic;
using System.Text;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
   public interface IApprenticeshipStandardSearchResult
    {
        ReferenceDateStandard Value { get; set; }
    }
}

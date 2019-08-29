using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData
{
    public class ReferenceDataFramework
    {
        public Guid Id { get; set; }
        public int  FrameworkCode{ get; set; }
        public int ProgType { get; set; }
        public int PathwayCode { get; set; }
        public string PathwayName { get; set; }
        public string NasTitle { get; set; }
        public string EffectiveFrom { get; set; }
        public string EffectiveTo { get; set; }
        public decimal SectorSubjectAreaTier1 { get; set; }
        public decimal SectorSubjectAreaTier2 { get; set; }
        public string CreatedDateTiemUtc { get; set; }
        public string ModifiedDateTiemUtc { get; set; }
        public int RecordStatusId { get; set; }
    }
}

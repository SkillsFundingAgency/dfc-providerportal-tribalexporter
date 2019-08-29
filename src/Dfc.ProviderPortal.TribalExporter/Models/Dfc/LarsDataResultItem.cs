using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;

namespace Dfc.ProviderPortal.TribalExporter.Models.Dfc
{
    public class LarsDataResultItem : ILarsDataResultItem
    {
        public string LearnAimRef { get; set; }
        public string LearnAimRefTitle { get; set; }
        public string NotionalNVQLevelv2 { get; set; }
        public string AwardOrgCode { get; set; }
        public string LearnAimRefTypeDesc { get; set; }
        public DateTime? CertificationEndDate { get; set; }
    }
}

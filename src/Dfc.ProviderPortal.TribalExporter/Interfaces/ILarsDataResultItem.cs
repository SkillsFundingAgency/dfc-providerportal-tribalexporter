using System;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ILarsDataResultItem
    {
        string LearnAimRef { get; set; }
        string LearnAimRefTitle { get; set; }
        string NotionalNVQLevelv2 { get; set; }
        string AwardOrgCode { get; set; }
        string LearnAimRefTypeDesc { get; set; }
        DateTime? CertificationEndDate { get; set; }
    }
}

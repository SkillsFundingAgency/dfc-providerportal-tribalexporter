using Dfc.CourseDirectory.Models.Enums;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class ProviderSource
    {
        public int ProviderId { get; set; }
        public int UKPRN { get; set; }
        public string ProviderName { get; set; }
        public TribalRecordStatus RecordStatusEnum { get; set; }
        public bool RoATPFFlag { get; set; }
        public int? RoATPProviderTypeId { get; set; }
        public DateTime? RoATPStartDate { get; set; }
        public bool PassedOverallQAChecks { get; set; }
        public string MarketingInformation { get; set; }
        public bool NationalApprenticeshipProvider { get; set; }
        public string TradingName { get; set; }
        public int? UPIN { get; set; }
        public int HasCourse { get; set; }
        public int HasApprenticeship { get; set; }

        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string Town { get; set; }
        public string County { get; set; }
        public string PostCode { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Telephone { get; set; }

        public bool IsValidAddress
        {
            get
            {
                return (!string.IsNullOrWhiteSpace(AddressLine1) 
                            && !string.IsNullOrWhiteSpace(Town) 
                            && !string.IsNullOrWhiteSpace(PostCode));
            }
        }

        public static ProviderSource FromDataReader(SqlDataReader reader)
        {
            var item = new ProviderSource();
            item.ProviderId = (int)reader["ProviderId"];
            item.ProviderName = reader["ProviderName"] as string;
            item.UKPRN = (int)reader["Ukprn"];
            item.RecordStatusEnum = (TribalRecordStatus)Enum.Parse(typeof(TribalRecordStatus), reader["RecordStatusId"].ToString());
            item.RoATPFFlag = (bool)reader["RoATPFFlag"];
            item.RoATPProviderTypeId = (reader["RoATPProviderTypeId"] == DBNull.Value) ? null : (int?)reader["RoATPProviderTypeId"];
            item.RoATPStartDate = (reader["RoATPStartDate"] == DBNull.Value) ? null : (DateTime?)reader["RoATPStartDate"];
            item.PassedOverallQAChecks = (bool)reader["PassedOverallQAChecks"];
            item.MarketingInformation = reader["MarketingInformation"] as string;
            item.NationalApprenticeshipProvider = (bool)reader["NationalApprenticeshipProvider"];
            item.TradingName = reader["TradingName"] as string;
            item.UPIN = (reader["UPIN"] == DBNull.Value) ? null : (int?)reader["UPIN"];
            item.HasCourse = (int)reader["HasCourse"];
            item.HasApprenticeship = (int)reader["HasApprenticeship"];
            item.HasApprenticeship = (int)reader["HasApprenticeship"];
            item.AddressLine1 = reader["AddressLine1"] as string;
            item.AddressLine1 = reader["AddressLine2"] as string;
            item.Town = reader["Town"] as string;
            item.County = reader["County"] as string;
            item.PostCode = reader["PostCode"] as string;
            item.Email = reader["Email"] as string;
            item.Website = reader["Website"] as string;
            item.Telephone = reader["Telephone"] as string;

            return item;
        }
    }
}

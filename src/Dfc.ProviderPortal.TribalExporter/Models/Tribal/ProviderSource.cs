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
                return item;
        }
    }
}

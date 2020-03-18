using Dfc.CourseDirectory.Models.Enums;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class FEChoicesSourceData
    {
        public int ProviderId { get; set; }
        public int UKPRN { get; set; }
        public int UPIN { get; set; }
        public double? LearnerSatisfaction { get; set; }
        public double? LearnerDestination { get; set; }
        public double? EmployerSatisfaction { get; set; }

        public DateTime CreatedDateTimeUtc { get; set; }


        public static FEChoicesSourceData FromDataReader(SqlDataReader reader)
        {
            var item = new FEChoicesSourceData();
            item.ProviderId = (int)reader["ProviderId"];
            item.UKPRN = (int)reader["Ukprn"];
            item.UPIN = (int)reader["UPIN"];
            item.LearnerSatisfaction = (reader["LearnerSatisfaction"] == DBNull.Value) ? null : (double?)reader["LearnerSatisfaction"];
            item.LearnerDestination = (reader["LearnerDestination"] == DBNull.Value) ? null : (double?)reader["LearnerDestination"];
            item.EmployerSatisfaction = (reader["EmployerSatisfaction"] == DBNull.Value) ? null : (double?)reader["EmployerSatisfaction"];
            item.CreatedDateTimeUtc = (DateTime)reader["CreatedDateTimeUtc"];


            return item;
        }
    }
}

using Dfc.CourseDirectory.Models.Enums;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Models.Tribal
{
    public class Venue
    {
        public string ID { get; set; }
        public int UKPRN { get; set; }
        public int VenueId { get; set; }
        public int ProviderId { get; set; }
        public string ProviderOwnVenueRef { get; set; }
        public string VenueName { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Fax { get; set; }
        public string Facilities { get; set; }
        public TribalRecordStatus RecordStatusId { get; set; }
        public string CreatedByUserId { get; set; }
        public DateTime CreatedDateTimeUtc { get; set; }
        public string ModifiedByUserId { get; set; }
        public DateTime? ModifiedDateTimeUtc { get; set; }
        public int AddressId { get; set; }
        public string Telephone { get; set; }
        public string BulkUploadVenueId { get; set; }
        public string CosmosId { get; set; }
        public int? AddedByApplicationId { get; set; }
        public Address Address { get; set; }
        public int? LocationID { get; set; }
        public VenueSource Source { get; set; }
 


        public static Venue FromDataReader(SqlDataReader reader)
        {
            var item = new Venue();
            item.VenueId = (int) reader["VenueId"];
            item.ProviderId = (int)reader["ProviderId"];
            item.ProviderOwnVenueRef = reader["ProviderOwnVenueRef"] as string;
            item.VenueName = reader["VenueName"] as string;
            item.Email = reader["Email"] as string;
            item.Website = reader["Website"] as string;
            item.Fax = reader["Fax"] as string;
            item.Facilities = reader["Facilities"] as string;
            item.RecordStatusId = (TribalRecordStatus) Enum.Parse(typeof(TribalRecordStatus), reader["RecordStatusId"].ToString());
            item.CreatedByUserId = reader["CreatedByUserId"] as string;
            item.CreatedDateTimeUtc = (DateTime)reader["CreatedDateTimeUtc"];
            item.ModifiedByUserId = reader["ModifiedByUserId"] as string;
            item.ModifiedDateTimeUtc = reader["ModifiedDateTimeUtc"] as DateTime?;
            item.AddressId = (int)reader["AddressId"];
            item.Telephone = reader["Telephone"] as string;
            item.BulkUploadVenueId = reader["BulkUploadVenueId"] as string;
            item.UKPRN = (int) reader["UKPRN"];
            item.LocationID = reader["LocationId"] as int?;
            item.Source = (VenueSource) Enum.Parse(typeof(VenueSource), reader["Source"].ToString());
            item.Address = new Address()
            {
                Address1 = reader["AddressLine1"] as string,
                Address2 = reader["AddressLine2"] as string,
                County = reader["County"] as string,
                Latitude = reader["Latitude"] as double?,
                Longitude = reader["Longitude"] as double?,
                Postcode = reader["Postcode"] as string,
                Town = reader["Town"] as string
            };

            return item;
        }
    }
}

using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Models.Interfaces.Venues;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Dfc.CourseDirectory.Models.Enums;
using System.ComponentModel;

namespace Dfc.CourseDirectory.Models.Models.Venues
{
    public enum VenueStatus
    {
        Undefined = 0,
        Live = 1,
        Pending = 2,
        Archived = 4,
        Deleted = 8,
        Uknown = 99
    }

    public class Venue : ValueObject<Venue>, IVenue
    {
        public int UKPRN { get; set; }
        [JsonProperty("PROVIDER_ID", Required = Required.AllowNull)]
        public int ProviderID { get; set; }
        [JsonProperty("VENUE_ID", Required = Required.AllowNull)]
        public int? VenueID { get; set; }
        [JsonProperty("VENUE_NAME")]
        public string VenueName { get; set; }
        [JsonProperty("PROV_VENUE_ID", Required = Required.AllowNull)]
        public string ProvVenueID { get; set; }
        [JsonProperty("ADDRESS_1")]
        public string Address1 { get; set; }
        [JsonProperty("ADDRESS_2")]
        public string Address2 { get; set; }
        [JsonProperty("TOWN")]
        public string Town { get; set; }
        [JsonProperty("COUNTY")]
        public string County { get; set; }
        [JsonProperty("POSTCODE")]
        public string PostCode { get; set; }
        [JsonProperty("LATITUDE")]
        public double? Latitude { get; set; }
        [JsonProperty("LONGITUDE")]
        public double? Longitude { get; set; }
        public VenueStatus Status { get; set; }
        public DateTime DateUpdated { get; set; }
        public string UpdatedBy { get; set; }

        // Apprenticeship related
        public string PHONE { get; set; }
        public string EMAIL { get; set; }
        public string WEBSITE { get; set; }

        public long? LocationId { get; set; }
        public int? TribalLocationId { get; set; }
        public string Telephone { get { return PHONE; } set { PHONE = value; } }
        public string Email { get { return EMAIL; } set { EMAIL = value; } }
        public string Website { get { return WEBSITE; } set { WEBSITE = value; } }
        [JsonProperty("id")]
        public string ID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public Venue()
        {
        }

        public Venue(
            int ukPrn,
            string venueName,
            string address1,
            string address2,
            string town,
            string county,
            string postcode,
            double? latitude,
            double? longitude,
            VenueStatus status,
            string updatedBy,
            DateTime dateUpdated)
        {
            //Throw.IfNullOrWhiteSpace(id, nameof(id));
            Throw.IfLessThan(0, ukPrn, nameof(ukPrn));
            Throw.IfNullOrWhiteSpace(venueName, nameof(VenueName));
            Throw.IfNullOrWhiteSpace(address1, nameof(address1));
            Throw.IfNullOrWhiteSpace(town, nameof(town));
            Throw.IfNullOrWhiteSpace(postcode, nameof(postcode));

            UKPRN = ukPrn;
            VenueName = venueName;
            Address1 = address1;
            Address2 = address2;
            Town = town;
            County = county;
            PostCode = postcode;
            Latitude = latitude;
            Longitude = longitude;
            Status = status;
            UpdatedBy = updatedBy;
            DateUpdated = dateUpdated;

        }


        //[JsonConstructor]
        public Venue(
            string id,
            int ukPrn,
            int providerID,
            int venueID,
            string venueName,
            string provVenueID,
            string address1,
            string address2,
            string address3,
            string town,
            string county,
            string postcode,
            double? latitude,
            double? longitude,
            VenueStatus status,
            string updatedBy,
            DateTime dateAdded,
            DateTime dateUpdated)
        {
            Throw.IfNullOrWhiteSpace(id, nameof(id));
            Throw.IfLessThan(0, ukPrn, nameof(ukPrn));
            //Throw.IfLessThan(0, providerID, nameof(providerID));
            //Throw.IfLessThan(0, venueID, nameof(venueID));
           //Throw.IfNullOrWhiteSpace(venueName, nameof(venueName));
            //Throw.IfNullOrWhiteSpace(provVenueID, nameof(provVenueID));
            ////Throw.IfNullOrWhiteSpace(address1, nameof(address1));
            //Throw.IfNullOrWhiteSpace(postcode, nameof(postcode));
            //Throw.IfNullOrWhiteSpace(updatedBy, nameof(updatedBy));

            UKPRN = ukPrn;
            ProviderID = providerID;
            VenueID = venueID;
            VenueName = venueName;
            ProvVenueID = provVenueID;
            Address1 = address1;
            Address2 = address2;
            Town = town;
            County = county;
            PostCode = postcode;
            Latitude = latitude;
            Longitude = longitude;
            Status = status;
            UpdatedBy = updatedBy;
            DateUpdated = dateUpdated;

        }


        public Venue(
            int ukPrn,
            string venueName,
            string address1,
            string address2,
            string address3,
            string town,
            string county,
            string postcode,
            double? latitude,
            double? longitude,
            VenueStatus status,
            string updatedBy,
            DateTime dateUpdated
            )
        {
            Throw.IfLessThan(0, ukPrn, nameof(ukPrn));
            //Throw.IfLessThan(0, providerID, nameof(providerID));
            //Throw.IfLessThan(0, venueID, nameof(venueID));
            //Throw.IfNullOrWhiteSpace(venueName, nameof(venueName));
            //Throw.IfNullOrWhiteSpace(provVenueID, nameof(provVenueID));
            //Throw.IfNullOrWhiteSpace(address1, nameof(address1));
            //Throw.IfNullOrWhiteSpace(address1, nameof(address2));
            //Throw.IfNullOrWhiteSpace(address1, nameof(address3));
            //Throw.IfNullOrWhiteSpace(town, nameof(town));
            //Throw.IfNullOrWhiteSpace(postcode, nameof(postcode));
            //Throw.IfNullOrWhiteSpace(updatedBy, nameof(updatedBy));

            UKPRN = ukPrn;
            VenueName = venueName;
            Address1 = address1;
            Address2 = address2;
            Town = town;
            County = county;
            PostCode = postcode;
            Latitude = latitude;
            Longitude = longitude;
            Status = status;
            UpdatedBy = updatedBy;
            DateUpdated = dateUpdated;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return ID;
            yield return UKPRN;
            yield return ProviderID;
            yield return VenueID;
            yield return VenueName;
            yield return ProvVenueID;
            yield return Address1;
            yield return Address2;
            yield return Town;
            yield return County;
            yield return PostCode;
            yield return Latitude;
            yield return Longitude;
            yield return Status;
            yield return UpdatedBy;
            yield return DateUpdated;
        }
    }
}

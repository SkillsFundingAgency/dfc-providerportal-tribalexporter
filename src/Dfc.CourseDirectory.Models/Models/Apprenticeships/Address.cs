﻿
using Dfc.CourseDirectory.Models.Interfaces.Apprenticeships;

namespace Dfc.CourseDirectory.Models.Models.Apprenticeships
{
    public class Address : IAddress
    {
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string County { get; set; }
        public string Email { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; }
        public string Postcode { get; set; }
        public string Town { get; set; }
        public string Website { get; set; }
    }
}

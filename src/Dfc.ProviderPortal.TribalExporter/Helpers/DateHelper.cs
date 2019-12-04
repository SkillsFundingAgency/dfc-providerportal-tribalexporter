using System;
using System.Globalization;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class DateHelper
    {
        public static DateTime ParseUkDateOrDefault(this string input, DateTime defaultDate)
        {
            var timeFormat = new CultureInfo("en-GB");
            if (string.IsNullOrEmpty(input)) return defaultDate;
            try
            {
                return Convert.ToDateTime(input, timeFormat);
            }
            catch
            {
                return defaultDate;
            }
        }

        public static double DaysBetween(this DateTime startDate, DateTime endDate)
        {
            var days = (endDate - startDate).TotalDays;
            return Math.Ceiling(days);
        }
    }
}
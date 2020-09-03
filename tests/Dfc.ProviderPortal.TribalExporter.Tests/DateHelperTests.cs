using System;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using FluentAssertions;
using Xunit;

namespace Dfc.ProviderPortal.TribalExporter.Tests
{
    public class DateHelperTests
    {
        [Theory]
        [InlineData("01/02/2019", "01/03/2019", 28)]
        [InlineData("2019-01-02 08:00", "2019-01-02 09:00", 1)]
        public void ParseUkDateOrDefault_ReturnsCorrectNumberOfDays(string start, string end, int expected)
        {
            DateTime startDate = start.ParseUkDateOrDefault(DateTime.Today);
            DateTime endDate = end.ParseUkDateOrDefault(DateTime.Today);
            double daysBetween = startDate.DaysBetween(endDate);

            daysBetween.Should().Be(expected);
        }

        [Theory]
        [InlineData("31/03/1982", "31/03/1982 12:00")]
        [InlineData("1982-03-31", "31/03/1982 12:00")]
        [InlineData("2019-12-01", "01/12/2019 12:00")]
        public void ParseUkDateOrDefault_ReturnsDate(string validDate, string expectedDate)
        {
            DateTime date = validDate.ParseUkDateOrDefault(DateTime.Today);
            string parsedDate = $"{date:dd/MM/yyyy hh:mm}";

            parsedDate.Should().Be(expectedDate);
        }

        [Theory]
        [InlineData("03/31/1982")]
        [InlineData("1982-31-03")]
        [InlineData("")]
        [InlineData("wibble")]
        [InlineData("really long text string that someone has put in here for no apparent reason")]
        [InlineData(null)]
        public void ParseUkDateOrDefault_ReturnsDefault(string invalidDate)
        {
            DateTime defaultDate = new DateTime(1999, 01, 01);
            DateTime parsedDate = invalidDate.ParseUkDateOrDefault(defaultDate);

            parsedDate.Should().Be(defaultDate);
        }
    }
}

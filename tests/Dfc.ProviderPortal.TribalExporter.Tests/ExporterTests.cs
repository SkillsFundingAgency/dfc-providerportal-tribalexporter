
using System.Collections;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using NSubstitute.Core.Arguments;

namespace Dfc.ProviderPortal.TribalExporter.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Xunit;

    using Dfc.ProviderPortal.TribalExporter.Services;


    public class ExporterTests : IDisposable
    {
        private static ExecutionContext _currentContext;

        public ExporterTests()
        {
            _currentContext = Substitute.For<ExecutionContext>();
            _currentContext.FunctionAppDirectory = "/";
        }

        public class DateHelperTests : ExporterTests
        {
            public DateHelperTests()
            {

            }

            public class DaysBetween : DateHelperTests
            {
                public DaysBetween()
                {

                }

                [Theory]
                [InlineData("01/02/2019", "01/03/2019", 28)]
                [InlineData("2019-01-02 08:00", "2019-01-02 09:00", 1)]
                public void ReturnsCorrectNumberOfDays(string start, string end, int expected)
                {
                    // Act
                    DateTime startDate = start.ParseUkDateOrDefault(DateTime.Today);
                    DateTime endDate = end.ParseUkDateOrDefault(DateTime.Today);
                    var actual = startDate.DaysBetween(endDate);

                    // Assert
                    Assert.Equal(expected, actual);
                }


            }

            public class ParseDateOrDefault : DateHelperTests
            {
                public ParseDateOrDefault()
                {

                }

                [Theory]
                [InlineData("31/03/1982", "31/03/1982 12:00")]
                [InlineData("1982-03-31", "31/03/1982 12:00")]
                [InlineData("2019-12-01", "01/12/2019 12:00")]
                public void ReturnsCorrectDateIfDateCanBeParsed(string input, string expected)
                {
                    // Act
                    DateTime date = input.ParseUkDateOrDefault(DateTime.Today);
                    var actual = $"{date:dd/MM/yyyy hh:mm}";

                    // Assert
                    Assert.Equal(expected, actual);
                }

                [Theory]
                [InlineData("03/31/1982")]
                [InlineData("1982-31-03")]
                [InlineData("")]
                [InlineData("wibble")]
                [InlineData("really long text string that someone has put in here for no apparent reason")]
                [InlineData(null)]
                public void ReturnsDefaultIfDateCannotBeParsed(string input)
                {
                    // Arrange
                    DateTime defaultDate = new DateTime(1999, 01, 01);

                    // Act
                    DateTime actual = input.ParseUkDateOrDefault(defaultDate);
                    var expected = defaultDate;

                    // Assert
                    Assert.Equal(expected, actual);
                }
            }


        }

        public class DateBatchTests : ExporterTests
        {
            public DateBatchTests()
            {

            }
        }
        
        public void Dispose()
        {
        }
    }
}

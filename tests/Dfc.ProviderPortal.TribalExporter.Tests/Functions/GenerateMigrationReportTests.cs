using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Services;
using FluentAssertions;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Xunit;

namespace Dfc.ProviderPortal.TribalExporter.Tests.Functions
{
    public class GenerateMigrationReportTests
    {
        // todo: test updating existing report
        // todo: generating new report
        // todo: errors get logged to report file
        [Fact]
        public void GenerateMigrationReport_Run()
        {
            // Arrange
            const string whiteListFileName = "ProviderWhiteList.txt";

            var testProviders = new List<Provider>
            {
                new Provider(null, null, null)
                {
                    UnitedKingdomProviderReferenceNumber = "1",
                    ProviderType = ProviderType.FE,
                },
                new Provider(null, null, null)
                {
                    UnitedKingdomProviderReferenceNumber = "2",
                    ProviderType = ProviderType.Apprenticeship,
                },
                new Provider(null, null, null)
                {
                    UnitedKingdomProviderReferenceNumber = "3",
                    ProviderType = ProviderType.Both,
                },
                new Provider(null, null, null)
                {
                    UnitedKingdomProviderReferenceNumber = "4",
                    ProviderType = ProviderType.Undefined,
                },
            };

            var mockBlobStorageHelper = new Mock<IBlobStorageHelper>();

            mockBlobStorageHelper.Setup(m => m.ReadFileAsync(It.IsAny<CloudBlobContainer>(), whiteListFileName))
                .ReturnsAsync(string.Join(Environment.NewLine,
                    testProviders.Select(p => p.UnitedKingdomProviderReferenceNumber)));

            byte[] uploadedLogData = null;
            mockBlobStorageHelper.Setup(m => m.UploadFile(It.IsAny<CloudBlobContainer>(),
                    It.IsRegex(@"MigrationReport_LogFile-.*\.txt"), It.IsAny<byte[]>()))
                .Callback((CloudBlobContainer b, string f, byte[] logData) => uploadedLogData = logData);

            var mockProviderCollectionService = new Mock<IProviderCollectionService>();

            mockProviderCollectionService.Setup(m => m.GetDocumentsByUkprn(It.IsAny<List<int>>()))
                .ReturnsAsync(testProviders);

            mockProviderCollectionService.Setup(m => m.GetAllMigratedProviders(It.IsAny<string>()))
                .ReturnsAsync(new List<Provider>());

            var mockApprenticeshipCollectionService = new Mock<IApprenticeshipCollectionService>();
            mockApprenticeshipCollectionService.Setup(m => m.GetAllApprenticeshipsByUkprnAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Apprenticeship>
                {
                    new Apprenticeship
                    {
                        ApprenticeshipLocations = new List<ApprenticeshipLocation>
                        {
                            new ApprenticeshipLocation
                            {
                                RecordStatus = RecordStatus.MigrationPending,
                            },
                        },
                    },
                });

            var mockCourseCollectionService = new Mock<ICourseCollectionService>();
            mockCourseCollectionService.Setup(m => m.GetAllCoursesByUkprnAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<Course>
                {
                    new Course
                    {
                        CourseRuns = new List<CourseRun>
                        {
                            new CourseRun
                            {
                                RecordStatus = RecordStatus.MigrationPending,
                            }
                        }
                    },
                });

            var mockConfigurationRoot = new Mock<IConfigurationRoot>();
            mockConfigurationRoot.SetupGet(m => m[It.IsAny<string>()]).Returns("");

            var mockCosmosDbHelper = new Mock<ICosmosDbHelper>();
            mockCosmosDbHelper.Setup(m => m.GetClient())
                .Returns(new Mock<IDocumentClient>().Object);


            // Act
            new MigrationReportGeneratorService().Run(
                NullLogger.Instance,
                mockConfigurationRoot.Object,
                mockCosmosDbHelper.Object,
                mockBlobStorageHelper.Object,
                mockProviderCollectionService.Object,
                mockCourseCollectionService.Object,
                mockApprenticeshipCollectionService.Object,
                new Mock<IMigrationReportCollectionService>().Object
            );

            // Assert
            uploadedLogData.Should().NotBeNull();
            var log = Encoding.UTF8.GetString(uploadedLogData);
            const string errorPrefix = "Error creating report for";
            log.Should().NotContain(errorPrefix);
        }
    }
}

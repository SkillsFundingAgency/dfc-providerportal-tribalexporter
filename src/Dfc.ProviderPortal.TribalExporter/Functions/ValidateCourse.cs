using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
using Dfc.ProviderPortal.TribalExporter.Validators;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UkrlpService;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ValidateCourse
    {
        [FunctionName(nameof(ValidateCourse))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] ICourseCollectionService courseCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper,
                    [Inject] IUkrlpApiService ukrlpApiService
                    )
        {
            const string apprenticeshipAppName = "Validate.Course";

            var apprenticeshipValidationFileName = $"{apprenticeshipAppName}-{DateTime.Now.ToString("dd-MM-yy HHmm")}";
            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var validator = new CourseValidator();

            List<CourseValidationResult> validationEResult = new List<CourseValidationResult>();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
      
            var resultData = await courseCollectionService.GetAllCoursesAsync();

            foreach(var item in resultData)
            {
                //item.ContactEmail = "testing";
                //item.Url = "testing";
                //item.ContactWebsite = "testing";
                //item.ContactTelephone = "101101abc";
                //item.ApprenticeshipTitle = item.ApprenticeshipTitle + " @";

                var validationResult = validator.Validate(item);

                if(!validationResult.IsValid)
                {
                    foreach(var error in validationResult.Errors)
                    {
                        validationEResult.Add(new CourseValidationResult()
                        {
                            CourseId = item.CourseId,
                            ProviderUkprn = item.ProviderUKPRN,
                            FieldName = error.PropertyName,
                            ErrorMessage = error.ErrorMessage
                        });
                    }
                }
            }

            var resultsObjBytes = GetResultAsByteArray(validationEResult);
            await WriteResultsToBlobStorage(resultsObjBytes);

            stopWatch.Stop();

            byte[] GetResultAsByteArray(IList<CourseValidationResult> message)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<CourseValidationResult>(message);
                    }
                    return memoryStream.ToArray();
                }
            }

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, apprenticeshipValidationFileName, data);
            }
        }
    }
}

public class CourseValidationResult
{
    public int? CourseId { get; set; }
    public int ProviderUkprn { get; set; }
    public string FieldName { get; set; }
    public string ErrorMessage { get; set; }
}

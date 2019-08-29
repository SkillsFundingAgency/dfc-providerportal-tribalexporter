using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Common.Interfaces;
using Dfc.CourseDirectory.Models.Interfaces.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Services.ApprenticeshipService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    public class ApprenticeshipService : IApprenticeshipService

    {
        private readonly ILogger<CourseService.CourseService> _logger;
        private readonly HttpClient _httpClient;
        private readonly Uri _addCourseUri;
        private readonly Uri _deleteApprenticeshipsByUKPRNUri;
        private readonly Uri _addApprenticeshipMigrationReportsUri;

        public ApprenticeshipService(ILogger<CourseService.CourseService> logger,
            HttpClient httpClient,
            IOptions<ApprenticeshipServiceSettings> settings)
        {
            _logger = logger;
            _httpClient = httpClient;
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.Value.ApiKey);
            _addCourseUri = settings.Value.ToAddApprenticeshipUri();
            _deleteApprenticeshipsByUKPRNUri = settings.Value.ToDeleteApprenticeshipsByUKPRNUri();
            _addApprenticeshipMigrationReportsUri = settings.Value.ToAddApprenticeshipMigrationReport();
        }

        public async Task<IResult<IApprenticeship>> AddApprenticeshipAsync(IApprenticeship apprenticeship)
        {
            _logger.LogMethodEnter();
            Throw.IfNull(apprenticeship, nameof(apprenticeship));

            try
            {
                _logger.LogInformationObject("Apprenticeship add object.", apprenticeship);
                _logger.LogInformationObject("Apprenticeship add URI", _addCourseUri);

                var courseJson = JsonConvert.SerializeObject(apprenticeship);

                var content = new StringContent(courseJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_addCourseUri, content);

                _logger.LogHttpResponseMessage("Apprenticeship add service http response", response);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    _logger.LogInformationObject("Apprenticeship add service json response", json);


                    var apprenticeshipResult = JsonConvert.DeserializeObject<Apprenticeship>(json);


                    return Result.Ok<IApprenticeship>(apprenticeshipResult);
                }
                else if ((int)response.StatusCode == 429)
                {
                    return Result.Fail<IApprenticeship>(
                        "Apprenticeship add service unsuccessful http response - TooManyRequests");
                }
                else
                {
                    return Result.Fail<IApprenticeship>(
                        "Apprenticeship add service unsuccessful http response - ResponseStatusCode: " +
                        response.StatusCode);
                }
            }
            catch (HttpRequestException hre)
            {
                _logger.LogException("Apprenticeship add service http request error", hre);
                return Result.Fail<IApprenticeship>("Apprenticeship add service http request error.");
            }
            catch (Exception e)
            {
                _logger.LogException("Apprenticeship add service unknown error.", e);

                return Result.Fail<IApprenticeship>("Apprenticeship add service unknown error.");
            }
            finally
            {
                _logger.LogMethodExit();
            }
        }

        public async Task<IResult<List<string>>> DeleteApprenticeshipsByUKPRNAsync(int ukprn)
        {

            Throw.IfNull(ukprn, nameof(ukprn));
            Throw.IfLessThan(0, ukprn, nameof(ukprn));
            _logger.LogMethodEnter();

            try
            {
                _logger.LogInformationObject("Delete Apprenticeships By UKPRN criteria", ukprn);
                _logger.LogInformationObject("Delete Apprenticeships By UKPRN URI", _deleteApprenticeshipsByUKPRNUri);

                var response =
                    await _httpClient.PostAsync(
                        new Uri(_deleteApprenticeshipsByUKPRNUri.AbsoluteUri + "?UKPRN=" + ukprn), null);
                _logger.LogHttpResponseMessage("Delete Apprenticeships By UKPRN service http response", response);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    if (!json.StartsWith("["))
                        json = "[" + json + "]";

                    _logger.LogInformationObject("Delete Apprenticeships By UKPRN json response", json);
                    List<string> messagesList = JsonConvert.DeserializeObject<List<string>>(json);

                    return Result.Ok<List<string>>(messagesList);
                }
                else
                {
                    return Result.Fail<List<string>>(
                        "Delete Apprenticeships By UKPRN service unsuccessful http response");
                }

            }
            catch (HttpRequestException hre)
            {
                _logger.LogException("Delete Apprenticeships By UKPRN service http request error", hre);
                return Result.Fail<List<string>>("Delete Apprenticeships By UKPRN service http request error.");

            }
            catch (Exception e)
            {
                _logger.LogException("Delete Apprenticeships By UKPRN service unknown error.", e);
                return Result.Fail<List<string>>("Delete Apprenticeships By UKPRN service unknown error.");

            }
            finally
            {
                _logger.LogMethodExit();
            }
        }
    }

    internal static class CourseServiceSettingsExtensions
    {
        internal static Uri ToAddApprenticeshipUri(this ApprenticeshipServiceSettings extendee)
        {
            return new Uri(extendee.ApiUrl + "AddApprenticeship");
        }

        internal static Uri ToDeleteApprenticeshipsByUKPRNUri(this ApprenticeshipServiceSettings extendee)
        {
            return new Uri(extendee.ApiUrl + "DeleteApprenticeshipsByUKPRN");
        }

        internal static Uri ToAddApprenticeshipMigrationReport(this ApprenticeshipServiceSettings extendee)
        {
            return new Uri(extendee.ApiUrl + "UpdateCourseMigrationReport");
        }
    }
}

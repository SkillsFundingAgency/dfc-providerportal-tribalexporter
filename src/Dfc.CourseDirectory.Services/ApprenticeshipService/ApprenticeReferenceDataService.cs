using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Common;
using Dfc.CourseDirectory.Common.Interfaces;
using Dfc.CourseDirectory.Models.Models.ApprenticeshipReferenceData;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.ProviderService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Dfc.CourseDirectory.Services.ApprenticeshipService
{
    public class ApprenticeReferenceDataService : IApprenticeReferenceDataService
    {
        private Uri _getFrameworkUri;
        private Uri _getStandardUri;
        private readonly ILogger<ApprenticeReferenceDataService> _logger;
        private readonly HttpClient _httpClient;
        public ApprenticeReferenceDataService(IOptions<ApprenticeReferenceDataSettings> settings, ILogger<ApprenticeReferenceDataService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.Value.ApiKey);
            _getFrameworkUri = new Uri(settings.Value.ApiUrl + "apprenticeship-frameworks/");
            _getStandardUri = new Uri(settings.Value.ApiUrl + "apprenticeship-standards/");
        }

        public async Task<IResult<IApprenticeshipFrameworkSearchResult>> GetFrameworkByCode(int code, int progType, int pathWayCode)
        {
            _logger.LogMethodEnter();

            try
            {
                _logger.LogInformationObject("Get Framework by id", code);
                _logger.LogInformationObject("Get Framework URI", _getFrameworkUri);

                var response = await _httpClient.GetAsync(_getFrameworkUri +
                                                          $"{code.ToString()}/prog-type/{progType}/pathway-code/{pathWayCode}");

                _logger.LogHttpResponseMessage("Get Framework service http response", response);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    _logger.LogInformationObject("Get Standard service json response", json);

                    var framework = JsonConvert.DeserializeObject<ReferenceDataFramework>(json);

                    var searchResult = new ApprenticeshipFrameworkSearchResult(framework)
                    {
                        Value = framework
                    };

                    return Result.Ok<IApprenticeshipFrameworkSearchResult>(searchResult);
                }
                else
                {
                    return Result.Fail<IApprenticeshipFrameworkSearchResult>("Get Framework service unsuccessful http response");
                }
            }
            catch (HttpRequestException hre)
            {
                _logger.LogException("Get Framework service http request error", hre);
                return Result.Fail<IApprenticeshipFrameworkSearchResult>("Get Framework service http request error.");
            }
            catch (Exception e)
            {
                _logger.LogException("Get Framework service unknown error.", e);

                return Result.Fail<IApprenticeshipFrameworkSearchResult>("Get Framework service unknown error.");
            }
            finally
            {
                _logger.LogMethodExit();
            }
        }


        public async Task<IResult<IApprenticeshipStandardSearchResult>> GetStandardById(int code, int version)
        {
            _logger.LogMethodEnter();

            try
            {
                _logger.LogInformationObject("Get Standard by id", code);
                _logger.LogInformationObject("Get Standard URI", _getFrameworkUri);

                var response = await _httpClient.GetAsync(_getStandardUri +
                                                          $"{code}/version/{version}");

                _logger.LogHttpResponseMessage("Get Standard service http response", response);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    _logger.LogInformationObject("Get Standard service json response", json);

                    var standard = JsonConvert.DeserializeObject<ReferenceDateStandard>(json);

                    var searchResult = new ApprenticeshipStandardSearchResult(standard)
                    {
                        Value = standard
                    };

                    return Result.Ok<IApprenticeshipStandardSearchResult>(searchResult);
                }
                else
                {
                    return Result.Fail<IApprenticeshipStandardSearchResult>("Get Standard service unsuccessful http response");
                }
            }
            catch (HttpRequestException hre)
            {
                _logger.LogException("Get Standard service http request error", hre);
                return Result.Fail<IApprenticeshipStandardSearchResult>("Get Standard service http request error.");
            }
            catch (Exception e)
            {
                _logger.LogException("Get Standard service unknown error.", e);

                return Result.Fail<IApprenticeshipStandardSearchResult>("Get Standard service unknown error.");
            }
            finally
            {
                _logger.LogMethodExit();
            }

        }
    }
}

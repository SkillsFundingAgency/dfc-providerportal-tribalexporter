using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter;
using Dfc.ProviderPortal.TribalExporter.Helpers;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Dfc.CourseDirectory.Common.Settings;
using Dfc.CourseDirectory.Models.Enums;
using Dfc.CourseDirectory.Services;
using Dfc.CourseDirectory.Services.ApprenticeshipService;
using Dfc.CourseDirectory.Services.BlobStorageService;
using Dfc.CourseDirectory.Services.CourseService;
using Dfc.CourseDirectory.Services.CourseTextService;
using Dfc.CourseDirectory.Services.Interfaces;
using Dfc.CourseDirectory.Services.Interfaces.ApprenticeshipService;
using Dfc.CourseDirectory.Services.Interfaces.BlobStorageService;
using Dfc.CourseDirectory.Services.Interfaces.CourseService;
using Dfc.CourseDirectory.Services.Interfaces.CourseTextService;
using Dfc.CourseDirectory.Services.Interfaces.ProviderService;
using Dfc.CourseDirectory.Services.Interfaces.VenueService;
using Dfc.CourseDirectory.Services.ProviderService;
using Dfc.CourseDirectory.Services.VenueService;
using Dfc.ProviderPortal.ApprenticeshipMigration;
using Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces;
using Dfc.ProviderPortal.ApprenticeshipMigration.Models;
using Dfc.ProviderPortal.TribalExporter.Functions;
using Dfc.ProviderPortal.TribalExporter.Services;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApprenticeshipServiceSettings = Dfc.ProviderPortal.TribalExporter.Settings.ApprenticeshipServiceSettings;
using ILogger = Microsoft.Build.Framework.ILogger;

[assembly: WebJobsStartup(typeof(WebJobsExtensionStartup), "Web Jobs Extension Startup")]

namespace Dfc.ProviderPortal.TribalExporter
{
    public class WebJobsExtensionStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddDependencyInjection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            builder.Services.AddSingleton<IConfigurationRoot>(configuration);
            builder.Services.Configure<CosmosDbSettings>(configuration.GetSection(nameof(CosmosDbSettings)));
            builder.Services.Configure<CosmosDbCollectionSettings>(configuration.GetSection(nameof(CosmosDbCollectionSettings)));
            //Update Settings for this
            builder.Services.Configure<BlobStorageDirectConnectionSettings>(configuration.GetSection(nameof(BlobStorageDirectConnectionSettings)));
            builder.Services.Configure<ApprenticeshipServiceSettings>(configuration.GetSection(nameof(ApprenticeshipServiceSettings)));
            builder.Services.AddScoped<ICosmosDbHelper, CosmosDbHelper>();
            builder.Services.AddScoped<IBlobStorageHelper, BlobStorageHelper>();
            builder.Services.AddScoped<IProviderCollectionService, ProviderCollectionService>();
            builder.Services.AddScoped<ICourseCollectionService, CourseCollectionService>();
            builder.Services.AddScoped<IVenueCollectionService, VenueCollectionService>();
            builder.Services.AddScoped<IApprenticeshipServiceWrapper, ApprenticeshipServiceWrapper>();

            builder.Services.AddLogging();
            builder.Services.AddTransient((provider) => new HttpClient());
            builder.Services.Configure<VenueServiceSettings>(venueServiceSettingsOptions =>
            {
                venueServiceSettingsOptions.ApiUrl = configuration.GetValue<string>("VenueServiceSettings:ApiUrl");
                venueServiceSettingsOptions.ApiKey = configuration.GetValue<string>("VenueServiceSettings:ApiKey");
            });
            builder.Services.AddScoped<IVenueService, VenueService>();
            builder.Services.Configure<ProviderServiceSettings>(providerServiceSettingsOptions =>
            {
                providerServiceSettingsOptions.ApiUrl =
                    configuration.GetValue<string>("ProviderServiceSettings:ApiUrl");
                providerServiceSettingsOptions.ApiKey =
                    configuration.GetValue<string>("ProviderServiceSettings:ApiKey");
            });
            builder.Services.AddScoped<IProviderService, ProviderService>();
            builder.Services.Configure<LarsSearchSettings>(larsSearchSettingsOptions =>
            {
                larsSearchSettingsOptions.ApiUrl = configuration.GetValue<string>("LarsSearchSettings:ApiUrl");
                larsSearchSettingsOptions.ApiKey = configuration.GetValue<string>("LarsSearchSettings:ApiKey");
                larsSearchSettingsOptions.ApiVersion = configuration.GetValue<string>("LarsSearchSettings:ApiVersion");
                larsSearchSettingsOptions.Indexes = configuration.GetValue<string>("LarsSearchSettings:Indexes");
                larsSearchSettingsOptions.ItemsPerPage =
                    Convert.ToInt32(configuration.GetValue<string>("LarsSearchSettings:ItemsPerPage"));
                larsSearchSettingsOptions.PageParamName =
                    configuration.GetValue<string>("LarsSearchSettings:PageParamName");
            });
            builder.Services.AddScoped<ILarsSearchService, LarsSearchService>();
            builder.Services.Configure<CourseForComponentSettings>(CourseForComponentSettingsOptions =>
            {
                CourseForComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:CourseForComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<EntryRequirementsComponentSettings>(EntryRequirementsComponentSettingsOptions =>
            {
                EntryRequirementsComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>(
                        "AppUISettings:EntryRequirementsComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<WhatWillLearnComponentSettings>(WhatWillLearnComponentSettingsOptions =>
            {
                WhatWillLearnComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:WhatWillLearnComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<HowYouWillLearnComponentSettings>(HowYouWillLearnComponentSettingsOptions =>
            {
                HowYouWillLearnComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:HowYouWillLearnComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<WhatYouNeedComponentSettings>(WhatYouNeedComponentSettingsOptions =>
            {
                WhatYouNeedComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:WhatYouNeedComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<HowAssessedComponentSettings>(HowAssessedComponentSettingsOptions =>
            {
                HowAssessedComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:HowAssessedComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<WhereNextComponentSettings>(WhereNextComponentSettingsOptions =>
            {
                WhereNextComponentSettingsOptions.TextFieldMaxChars =
                    configuration.GetValue<int>("AppUISettings:WhereNextComponentSettings:TextFieldMaxChars");
            });
            builder.Services.Configure<CourseServiceSettings>(courseServiceSettingsOptions =>
            {
                courseServiceSettingsOptions.ApiUrl = configuration.GetValue<string>("CourseServiceSettings:ApiUrl");
                courseServiceSettingsOptions.ApiKey = string.IsNullOrEmpty(configuration.GetValue<string>("CourseServiceSettings:ApiKey"))?
                    configuration.GetValue<string>("CourseServiceSettings_ApiKey") :
                    configuration.GetValue<string>("CourseServiceSettings:ApiKey");
            });
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.Configure<CourseTextServiceSettings>(courseTextServiceSettingsOptions =>
            {
                courseTextServiceSettingsOptions.ApiUrl =
                    configuration.GetValue<string>("CourseTextServiceSettings:ApiUrl");
                courseTextServiceSettingsOptions.ApiKey =
                    configuration.GetValue<string>("CourseTextServiceSettings:ApiKey");
            });
            builder.Services.AddScoped<ICourseTextService, CourseTextService>();
            builder.Services.Configure<BlobStorageSettings>(options =>
            {
                options.AccountName = configuration.GetValue<string>("BlobStorageSettings:AccountName");
                options.AccountKey = configuration.GetValue<string>("BlobStorageSettings:AccountKey");
                options.Container = configuration.GetValue<string>("BlobStorageSettings:Container");
                options.TemplatePath = configuration.GetValue<string>("BlobStorageSettings:TemplatePath");
                options.ProviderListPath = configuration.GetValue<string>("BlobSettings:ProviderListPath");
            });
            builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
            builder.Services.Configure<CourseDirectory.Services.ApprenticeshipService.ApprenticeshipServiceSettings>(
                configuration.GetSection(nameof(CourseDirectory.Services.ApprenticeshipService
                    .ApprenticeshipServiceSettings)));
            builder.Services.Configure<ApprenticeReferenceDataSettings>(
                configuration.GetSection(nameof(ApprenticeReferenceDataSettings)));
            builder.Services.AddScoped<IApprenticeshipService, ApprenticeshipService>();
            builder.Services.AddScoped<IApprenticeReferenceDataService, ApprenticeReferenceDataService>();
            builder.Services.BuildServiceProvider();
            builder.Services.AddTransient<IApprenticeshipMigration, ApprenticeshipMigration.ApprenticeshipMigration>();
            builder.Services.AddTransient<BlobStorageServiceResolver>(serviceProvider => key =>
            {
                switch (key)
                {
                    case nameof(ApprenticeshipMigration.ApprenticeshipMigration):
                        return new BlobStorageService(
                            serviceProvider.GetService<ILogger<BlobStorageService>>(),
                            serviceProvider.GetService<HttpClient>(),
                            new BlobStorageSettings
                            {
                                AccountName = configuration.GetValue<string>("BlobStorageSettings:AccountName"),
                                AccountKey = configuration.GetValue<string>("BlobStorageSettings:AccountKey"),
                                Container = configuration.GetValue<string>("BlobStorageSettings:Container"),
                                TemplatePath = configuration.GetValue<string>("BlobStorageSettings:TemplatePath"),
                                ProviderListPath = configuration.GetValue<string>("BlobSettings:ApprenticeshipProviderListPath")
                            });
                    case nameof(FeCourseMigrationFunction):
                        return new BlobStorageService(
                            serviceProvider.GetService<ILogger<BlobStorageService>>(),
                            serviceProvider.GetService<HttpClient>(),
                            new BlobStorageSettings
                            {
                                AccountName = configuration.GetValue<string>("BlobStorageSettings:AccountName"),
                                AccountKey = configuration.GetValue<string>("BlobStorageSettings:AccountKey"),
                                Container = configuration.GetValue<string>("BlobStorageSettings:Container"),
                                TemplatePath = configuration.GetValue<string>("BlobStorageSettings:TemplatePath"),
                                ProviderListPath = configuration.GetValue<string>("BlobStorageSettings:FeProviderListPath")
                            });
                    default:
                        throw new KeyNotFoundException(); // or maybe return null, up to you
                }
            });

            builder.Services.Configure<ApprenticeshipMigrationSettings>(settings =>
            {
                settings.ConnectionString = configuration.GetConnectionString("DefaultConnection");
                settings.GenerateJsonFilesLocally = configuration.GetValue<bool>("AutomatedMode");
                settings.GenerateReportFilesLocally = configuration.GetValue<bool>("GenerateReportFilesLocally");
                settings.DeleteCoursesByUKPRN = configuration.GetValue<bool>("DeleteCoursesByUKPRN");
                settings.DeploymentEnvironment = configuration.GetValue<DeploymentEnvironment>("DeploymentEnvironment");
                settings.JsonApprenticeshipFilesPath = configuration.GetValue<string>("JsonApprenticeshipFilesPath");
                settings.VenueBasedRadius = configuration.GetValue<int>("VenueBasedRadius");
                settings.RegionBasedRadius = configuration.GetValue<int>("RegionBasedRadius");
                settings.SubRegionBasedRadius = configuration.GetValue<int>("SubRegionBasedRadius");
                settings.RegionSubRegionRangeRadius = configuration.GetValue<int>("RegionSubRegionRangeRadius");
                settings.UpdateProvider = configuration.GetValue<bool>("UpdateProvider");
            });
        }
    }
}
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Microsoft.Azure.Documents.Linq;

namespace Dfc.ProviderPortal.TribalExporter.Functions
{
    public static class ArchivePendingApprenticeships
    {
        [FunctionName(nameof(ArchivePendingApprenticeships))]
        [NoAutomaticTrigger]
        public static async Task Run(
                                        string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                                        [Inject] IConfigurationRoot configuration,
                                        [Inject] ICosmosDbHelper cosmosDbHelper,
                                        [Inject] IBlobStorageHelper blobHelper,
                                        [Inject] ILoggerFactory loggerFactory,
                                        [Inject] IBlobStorageHelper blobhelper)
        {

            var blobContainer = configuration["BlobStorageSettings:Container"];
            var outputContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var apprenticeshipCollectionId = "apprenticeship";
            var documentClient = cosmosDbHelper.GetClient();
            var apprenticeshipCollectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, apprenticeshipCollectionId);
            var logger = loggerFactory.CreateLogger(typeof(ArchivePendingApprenticeships));
            int count = 0;
            var updatedBy = "ArchivePendingApprenticeships";

            var queryResponse = await documentClient.CreateDocumentQuery<Apprenticeship>(apprenticeshipCollectionUri)
                            .Where(p => p.RecordStatus == CourseDirectory.Models.Enums.RecordStatus.MigrationPending)
                            .AsDocumentQuery()
                            .ExecuteNextAsync<Apprenticeship>();

           foreach (var doc in queryResponse)
           {
               //mark every location as arhived
               foreach (var loc in doc.ApprenticeshipLocations)
               {
                   loc.RecordStatus = CourseDirectory.Models.Enums.RecordStatus.Archived;
                   loc.UpdatedBy = updatedBy;
               }
               doc.UpdatedBy = updatedBy;

               var documentLink = UriFactory.CreateDocumentUri(databaseId, apprenticeshipCollectionId, doc.id.ToString());
               await documentClient.ReplaceDocumentAsync(documentLink, doc, new RequestOptions(){});

               count++;
           }
           
           logger.LogInformation($"Archived {count} Apprenticeships");
           Console.WriteLine($"Archived {count} Apprenticeships");

        }
    }
}

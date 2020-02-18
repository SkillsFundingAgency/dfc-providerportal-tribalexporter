using CsvHelper;
using Dfc.CourseDirectory.Models.Models.Providers;
using Dfc.ProviderPortal.Packages.AzureFunctions.DependencyInjection;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Models.Tribal;
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
    public static class ProviderMigrator
    {
        [FunctionName(nameof(ProviderMigrator))]
        [NoAutomaticTrigger]
        public static async Task Run(
                    string input,  // Work around https://github.com/Azure/azure-functions-vs-build-sdk/issues/168
                    ILogger log,
                    [Inject] IConfigurationRoot configuration,
                    [Inject] IProviderCollectionService providerCollectionService,
                    [Inject] ICosmosDbHelper cosmosDbHelper,
                    [Inject] IBlobStorageHelper blobhelper,
                    [Inject] IUkrlpApiService ukrlpApiService
                    )
        {
            const string WHITE_LIST_FILE = "ProviderWhiteList.txt";
            const string ProviderAppName = "Provider.Migrator";

            var stopWatch = new Stopwatch();

            // TODO : Change to correct collection below
            var databaseId = configuration["CosmosDbSettings:DatabaseId"];
            var providerCollectionId = configuration["CosmosDbCollectionSettings:ProvidersCollectionId"];
            var connectionString = configuration.GetConnectionString("TribalRestore");
            var venueExportFileName = $"ProviderExport-{DateTime.Now.ToString("dd-MM-yy HHmm")}";

            var blobContainer = blobhelper.GetBlobContainer(configuration["BlobStorageSettings:Container"]);

            log.LogInformation($"WhitelistProviders : Start loading...");
            var whiteListProviders = await GetProviderWhiteList();
            log.LogInformation($"WhitelistProviders : Finished loading.");

            // Get all changed data from UKRLP API

            stopWatch.Reset();
            log.LogInformation($"UKRLApiService: Start getting data..");
            stopWatch.Start();
            var ukrlpApiProviders = ukrlpApiService.GetAllProviders(whiteListProviders.Select(p => p.ToString()).ToList());
            stopWatch.Stop();
            log.LogInformation($"UKRLApiService: Finished getting datain {stopWatch.ElapsedMilliseconds / 1000}.");

            int totalTribalCount = 0;
            int totalAttemptedCount = 0;
            int totalUpdatedCount = 0;
            int totalInsertedCount = 0;
       
            var result = new List<ProviderResultMessage>();

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"SELECT 
                                                P.ProviderId,
                                                P.Ukprn,
                                                P.ProviderName,
                                                RS.RecordStatusId,
                                                RS.RecordStatusName,
		                                        P.RoATPFFlag,
		                                        P.RoATPProviderTypeId,
		                                        P.RoATPStartDate,
		                                        p.PassedOverallQAChecks,
		                                        P.MarketingInformation,
		                                        P.NationalApprenticeshipProvider,
		                                        P.TradingName,
		                                        P.UPIN,
		                                        CASE   
		                                          WHEN Count(C.CourseId) > 0 THEN 1 
		                                          WHEN Count(C.CourseId) = 0 THEN 0   
	                                            END As HasCourse,
				                                        CASE   
		                                          WHEN Count(A.ApprenticeshipId) > 0 THEN 1 
		                                          WHEN Count(A.ApprenticeshipId) = 0 THEN 0   
	                                            END As HasApprenticeship
                                        FROM [Provider] P
                                        JOIN [RecordStatus] RS
                                        ON P.RecordStatusId = RS.RecordStatusId
                                        LEFT JOIN [Course] C
                                        ON P.ProviderId = C.ProviderId
                                        LEFT JOIN [Apprenticeship] A
                                        ON P.ProviderId = A.ProviderId
                                        WHERE P.RecordStatusId = 2
                                        GROUP BY P.ProviderId,
                                                P.Ukprn,
                                                P.ProviderName,
                                                RS.RecordStatusId,
                                                RS.RecordStatusName,
		                                        P.RoATPFFlag,
		                                        P.RoATPProviderTypeId,
		                                        P.RoATPStartDate,
		                                        p.PassedOverallQAChecks,
		                                        P.MarketingInformation,
		                                        P.NationalApprenticeshipProvider,
		                                        P.TradingName,
		                                        P.UPIN
                                            ";

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        stopWatch.Reset();
                        log.LogInformation($"Tribal Data: Start....");
                        stopWatch.Start();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            using (var _cosmosClient = cosmosDbHelper.GetClient())
                            {
                                while (dataReader.Read())
                                {
                                    // 1) Read provider data from Tribal
                                    var item = ProviderSource.FromDataReader(dataReader);
                                    totalTribalCount++;

                                    try
                                    {
                                        // 2) Check if in Whitelist
                                        if (!whiteListProviders.Any(x => x == item.UKPRN))
                                        {
                                            AddResultMessage(item.ProviderId, "SKIPPED-NotOnWhitelist", $"Provider {item.ProviderId} not on whitelist, ukprn {item.UKPRN}");
                                            continue;
                                        }

                                        totalAttemptedCount++;

                                        // 3) Check againts API ? If no match Add to Result Message, skip next
                                        var ukrlpProviderItem = ukrlpApiProviders.FirstOrDefault(p => p.UnitedKingdomProviderReferenceNumber.Trim() == item.UKPRN.ToString());
                                        if (ukrlpProviderItem == null)
                                        {
                                            AddResultMessage(item.ProviderId, "SKIPPED-NotInUkrlpApi", $"Provider {item.ProviderId} cannot be found in UKRLP Api, ukprn {item.UKPRN}");
                                            continue;
                                        }

                                        // 4) Build Cosmos collection record
                                        var providerToUpsert = BuildNewCosmosProviderItem(ukrlpProviderItem, item);
                                        var cosmosProviderItem = await providerCollectionService.GetDocumentByUkprn(item.UKPRN);

                                        if (cosmosProviderItem != null)
                                        {
                                            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, providerCollectionId);

                                            await _cosmosClient.UpsertDocumentAsync(collectionUri, UpdateCosmosProviderItem(cosmosProviderItem, providerToUpsert));
                                            totalUpdatedCount++;

                                            AddResultMessage(item.ProviderId, "PROCESSED-Updated", $"Provider {item.ProviderId} updated in Cosmos Collection, ukprn {item.UKPRN}");
                                        }
                                        else
                                        {
                                            await cosmosDbHelper.CreateDocumentAsync(_cosmosClient, providerCollectionId, providerToUpsert);
                                            totalInsertedCount++;

                                            AddResultMessage(item.ProviderId, "PROCESSED-Inserted", $"Provider {item.ProviderId} inserted in Cosmos Collection, ukprn {item.UKPRN}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        string errorMessage = $"Error processing Provider {item.ProviderId} with Ukprn {item.UKPRN}. {ex.Message}";
                                        AddResultMessage(item.ProviderId, "PROCESSED-Errored", errorMessage);
                                        log.LogInformation(errorMessage);
                                    }
                                }
                            }
                            dataReader.Close();
                        }

                        stopWatch.Stop();
                        log.LogInformation($"Tribal Data: Processing completed in {stopWatch.ElapsedMilliseconds / 1000}");

                        AddResultMessage(0, "SUMMARY", $"Total Time : {stopWatch.ElapsedMilliseconds / 1000} seconds, Tribal : {totalTribalCount}, URLP : {ukrlpApiProviders.Count}, Processed : {totalAttemptedCount}, Updated : {totalUpdatedCount}, Inserted : {totalInsertedCount}");
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                    }

                    var resultsObjBytes = GetResultAsByteArray(result);
                    await WriteResultsToBlobStorage(resultsObjBytes);
                }
            }

            void AddResultMessage(int providerId, string status, string message = "")
            {
                var validateResult = new ProviderResultMessage() { ProviderId = providerId, Status = status, Message = message };
                result.Add(validateResult);
            }

            Provider BuildNewCosmosProviderItem(ProviderRecordStructure ukrlpData, ProviderSource tribalData)
             {
                // Build contacts
                List<Providercontact> providercontacts = new List<Providercontact>();
                var ukrlpDataContacts = ukrlpData.ProviderContact
                                                    .OrderByDescending(c => c.LastUpdated);

                if(!ukrlpDataContacts.Any())
                {
                    throw new Exception("Provider contacts of type P could not be found.");
                }

                foreach (ProviderContactStructure ukrlpContact in ukrlpDataContacts)
                {
                    // Build contact address
                    Contactaddress contactaddress = new Contactaddress()
                    {
                        SAON = new SAON() { Description = ukrlpContact.ContactAddress.SAON.Description },
                        PAON = new PAON() { Description = ukrlpContact.ContactAddress.PAON.Description },
                        StreetDescription = ukrlpContact.ContactAddress.StreetDescription,
                        UniqueStreetReferenceNumber = ukrlpContact.ContactAddress.UniqueStreetReferenceNumber,
                        UniquePropertyReferenceNumber = ukrlpContact.ContactAddress.UniquePropertyReferenceNumber,
                        Locality = ukrlpContact.ContactAddress.Locality,
                        Items = ukrlpContact.ContactAddress.Items,
                        ItemsElementName = ukrlpContact.ContactAddress.ItemsElementName?.Select(i => (int)i).ToArray(),
                        PostTown = ukrlpContact.ContactAddress.ItemsElementName,
                        PostCode = ukrlpContact.ContactAddress.PostCode,
                    };

                    // Build contact personal details
                    Contactpersonaldetails contactpersonaldetails = new Contactpersonaldetails()
                    {
                        PersonNameTitle = ukrlpContact.ContactPersonalDetails.PersonNameTitle,
                        PersonGivenName = ukrlpContact.ContactPersonalDetails.PersonGivenName,
                        PersonFamilyName = ukrlpContact.ContactPersonalDetails.PersonFamilyName,
                        PersonNameSuffix = ukrlpContact.ContactPersonalDetails.PersonNameSuffix,
                        PersonRequestedName = ukrlpContact.ContactPersonalDetails.PersonRequestedName,

                    };

                    var providerContact = new Providercontact(contactaddress, contactpersonaldetails);
                    providerContact.ContactType = ukrlpContact.ContactType;
                    providerContact.ContactRole = ukrlpContact.ContactRole;
                    providerContact.ContactTelephone1 = ukrlpContact.ContactTelephone1;
                    providerContact.ContactTelephone2 = ukrlpContact.ContactTelephone2;
                    providerContact.ContactWebsiteAddress = ukrlpContact.ContactWebsiteAddress;
                    providerContact.ContactEmail = ukrlpContact.ContactEmail;
                    providerContact.LastUpdated = ukrlpContact.LastUpdated;

                    providercontacts.Add(providerContact);
                }

                // Build provider aliases
                List<Provideralias> provideraliases = new List<Provideralias>();
                foreach (ProviderAliasesStructure ukrlpProviderAlias in ukrlpData.ProviderAliases)
                {
                    provideraliases.Add(new Provideralias()
                    {
                        ProviderAlias = ukrlpProviderAlias.ProviderAlias,
                        LastUpdated = ukrlpProviderAlias.LastUpdated,
                    });
                }

                // Build provider Verificationdetail
                List<Verificationdetail> providerVerificationdetails = new List<Verificationdetail>();
                foreach (VerificationDetailsStructure providerVerificationDetail in ukrlpData.VerificationDetails)
                {
                    providerVerificationdetails.Add(new Verificationdetail()
                    {
                        VerificationAuthority = providerVerificationDetail.VerificationAuthority,
                        VerificationID = providerVerificationDetail.VerificationID,
                    });
                }

                Provider providerToUpsert = new Provider(providercontacts.ToArray(), provideraliases.ToArray(), providerVerificationdetails.ToArray());

                providerToUpsert.ProviderId = tribalData.ProviderId;
                providerToUpsert.id = Guid.NewGuid();
                providerToUpsert.UnitedKingdomProviderReferenceNumber = tribalData.UKPRN.ToString();
                providerToUpsert.ProviderType = GetProviderType(tribalData.HasCourse, tribalData.HasApprenticeship);
                providerToUpsert.ProviderName = ukrlpData.ProviderName;
                providerToUpsert.ProviderStatus = ukrlpData.ProviderStatus;
                providerToUpsert.ProviderVerificationDate = ukrlpData.ProviderVerificationDate;
                providerToUpsert.ProviderVerificationDateSpecified = ukrlpData.ProviderVerificationDateSpecified;
                providerToUpsert.ExpiryDateSpecified = ukrlpData.ExpiryDateSpecified;
                providerToUpsert.ProviderAssociations = ukrlpData.ProviderAssociations;
                providerToUpsert.Alias = ukrlpData.ProviderAliases?.FirstOrDefault()?.ProviderAlias;
                providerToUpsert.Status = Status.Onboarded; // TODO : is this correct ?
                providerToUpsert.PassedOverallQAChecks = tribalData.PassedOverallQAChecks;
                providerToUpsert.RoATPFFlag = tribalData.RoATPFFlag;
                providerToUpsert.RoATPProviderTypeId = tribalData.RoATPProviderTypeId;
                providerToUpsert.RoATPStartDate = tribalData.RoATPStartDate;
                providerToUpsert.MarketingInformation = tribalData.MarketingInformation;
                providerToUpsert.NationalApprenticeshipProvider = tribalData.NationalApprenticeshipProvider;
                providerToUpsert.TradingName = tribalData.TradingName;
                providerToUpsert.UPIN = tribalData.UPIN;


                providerToUpsert.LastUpdatedBy = ProviderAppName;
                providerToUpsert.DateUpdated = DateTime.UtcNow;

                return providerToUpsert;
            }

            Provider UpdateCosmosProviderItem(Provider cosmosProviderItem, Provider providerToUpsert)
            {
                cosmosProviderItem.Alias = providerToUpsert.Alias;
                cosmosProviderItem.ExpiryDateSpecified = providerToUpsert.ExpiryDateSpecified;
                cosmosProviderItem.MarketingInformation = providerToUpsert.MarketingInformation; 
                cosmosProviderItem.NationalApprenticeshipProvider = providerToUpsert.NationalApprenticeshipProvider; 
                cosmosProviderItem.PassedOverallQAChecks = providerToUpsert.PassedOverallQAChecks;
                cosmosProviderItem.ProviderAliases = providerToUpsert.ProviderAliases;
                cosmosProviderItem.ProviderAssociations = providerToUpsert.ProviderAssociations;
                cosmosProviderItem.ProviderContact = providerToUpsert.ProviderContact;
                cosmosProviderItem.ProviderId = providerToUpsert.ProviderId;
                cosmosProviderItem.ProviderName = providerToUpsert.ProviderName;
                cosmosProviderItem.ProviderStatus = providerToUpsert.ProviderStatus;
                cosmosProviderItem.ProviderType = providerToUpsert.ProviderType;
                cosmosProviderItem.ProviderVerificationDate = providerToUpsert.ProviderVerificationDate;
                cosmosProviderItem.ProviderVerificationDateSpecified = providerToUpsert.ProviderVerificationDateSpecified;
                cosmosProviderItem.RoATPFFlag = providerToUpsert.RoATPFFlag;
                cosmosProviderItem.RoATPProviderTypeId = providerToUpsert.RoATPProviderTypeId;
                cosmosProviderItem.RoATPStartDate = providerToUpsert.RoATPStartDate;
                cosmosProviderItem.Status = providerToUpsert.Status;
                cosmosProviderItem.TradingName = providerToUpsert.TradingName; 
                cosmosProviderItem.UnitedKingdomProviderReferenceNumber = providerToUpsert.UnitedKingdomProviderReferenceNumber;
                cosmosProviderItem.UPIN = providerToUpsert.UPIN;
                cosmosProviderItem.VerificationDetails = providerToUpsert.VerificationDetails;

                cosmosProviderItem.LastUpdatedBy = providerToUpsert.LastUpdatedBy;
                cosmosProviderItem.DateUpdated = providerToUpsert.DateUpdated;

                return cosmosProviderItem;
            }

            async Task<IList<int>> GetProviderWhiteList()
            {
                var list = new List<int>();
                var whiteList = await blobhelper.ReadFileAsync(blobContainer, WHITE_LIST_FILE);
                if (!string.IsNullOrEmpty(whiteList))
                {
                    var lines = whiteList.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (string line in lines)
                    {
                        if (int.TryParse(line, out int id))
                        {
                            list.Add(id);
                        }
                    }
                }
                return list;
            }

            byte[] GetResultAsByteArray(IList<ProviderResultMessage> message)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var streamWriter = new System.IO.StreamWriter(memoryStream))
                    using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                    {
                        csvWriter.WriteRecords<ProviderResultMessage>(message);
                    }
                    return memoryStream.ToArray();
                }
            }

            async Task WriteResultsToBlobStorage(byte[] data)
            {
                await blobhelper.UploadFile(blobContainer, venueExportFileName, data);
            }
        }

        private static ProviderType GetProviderType(int hasCourse, int hasApprenticeship)
        {
            if (hasCourse == 1 && hasApprenticeship ==1)
                return ProviderType.Both;
            else if (hasCourse == 1  && hasApprenticeship != 1)
                return ProviderType.FE;
            else if (hasCourse != 1 && hasApprenticeship == 1)
                return ProviderType.Apprenticeship;
            else
                return ProviderType.Undefined;
        }
    }
}

[Serializable()]
public class ProviderResultMessage
{
    public int ProviderId { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}

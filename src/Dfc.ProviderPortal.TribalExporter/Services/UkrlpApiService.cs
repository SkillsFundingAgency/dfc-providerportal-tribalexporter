using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkrlpService;

namespace Dfc.ProviderPortal.TribalExporter.Services
{
    public class UkrlpApiService : IUkrlpApiService
    {
        public UkrlpApiService()
        {

        }

        public List<ProviderRecordStructure> GetAllProviders(List<string> ukprnList)
        {
            string[] statusesToFetch =
             {
                    "A", // Active
                    //"V", // Verified. Omitted, we suspect this may be a subset of Active providers
                    //"PD1", // Deactivation in process
                    //"PD2" // Deactivation complete
            };

            List<ProviderRecordStructure> results = new List<ProviderRecordStructure>();

            int chunkSize = 300;
            var noOfChunks = Math.Ceiling((double)ukprnList.Count / chunkSize);

            // Get UKRLP data in chunks at a time as API does not support large requests.
            for (int i = 0; i < noOfChunks; i++)
            {
                foreach (String status in statusesToFetch)
                {
                    var request = BuildRequest(ukprnList.Skip(i * chunkSize).Take(chunkSize).ToArray());
                    request.SelectionCriteria.ProviderStatus = status;
                    request.QueryId = GetNextQueryId();

                    var providerClient = new ProviderQueryPortTypeClient();
                    providerClient.InnerChannel.OperationTimeout = new TimeSpan(0, 10, 0);
                    Task<response> x = providerClient.retrieveAllProvidersAsync(request);
                    x.Wait();

                    results.AddRange(x.Result?.ProviderQueryResponse?.MatchingProviderRecords ?? new ProviderRecordStructure[] { });
                }
            }

            return results.ToList();
        }

        private static String GetNextQueryId()
        {
            Int32 id = 0;
            id++;
            return id.ToString();
        }

        private ProviderQueryStructure BuildRequest(string[] ukprnListToFetch)
        {
            SelectionCriteriaStructure scs = new SelectionCriteriaStructure
            {
                StakeholderId = "1",
                UnitedKingdomProviderReferenceNumberList = ukprnListToFetch,
                ApprovedProvidersOnly = YesNoType.No,
                ApprovedProvidersOnlySpecified = true,
                CriteriaCondition = QueryCriteriaConditionType.OR,
                CriteriaConditionSpecified = true
            };

            return new ProviderQueryStructure { SelectionCriteria = scs };
        }
    }
}

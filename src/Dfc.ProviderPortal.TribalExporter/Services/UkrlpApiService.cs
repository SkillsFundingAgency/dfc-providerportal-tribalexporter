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

        public List<ProviderRecordStructure> GetAllProviders()
        {
            string[] statusesToFetch =
             {
                    "A", // Active
                    //"V", // Verified. Omitted, we suspect this may be a subset of Active providers
                    //"PD1", // Deactivation in process
                    //"PD2" // Deactivation complete
            };

            List<ProviderRecordStructure> results = new List<ProviderRecordStructure>();

            var request = BuildRequest();

            foreach (String status in statusesToFetch)
            {
                request.SelectionCriteria.ProviderStatus = status;
                request.QueryId = GetNextQueryId();

                var providerClient = new ProviderQueryPortTypeClient();
                providerClient.InnerChannel.OperationTimeout = new TimeSpan(0, 10, 0);
                Task<response> x = providerClient.retrieveAllProvidersAsync(request);
                x.Wait();

                results.AddRange(x.Result?.ProviderQueryResponse?.MatchingProviderRecords ?? new ProviderRecordStructure[] { });
            }

            return results.ToList();
        }

        private static String GetNextQueryId()
        {
            Int32 id = 0;
            id++;
            return id.ToString();
        }

        private ProviderQueryStructure BuildRequest()
        {
            SelectionCriteriaStructure scs = new SelectionCriteriaStructure
            {
                StakeholderId = "1",
                ProviderUpdatedSince = DateTime.Now.AddMonths(-12),
                ProviderUpdatedSinceSpecified = true,
                ApprovedProvidersOnly = YesNoType.No,
                ApprovedProvidersOnlySpecified = true,
                CriteriaCondition = QueryCriteriaConditionType.OR,
                CriteriaConditionSpecified = true
            };

            return new ProviderQueryStructure { SelectionCriteria = scs };
        }
    }
}

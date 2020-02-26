using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class StoredProcHelper
    {
        public static async Task UpdateArchiveCoursesForProvider(DocumentClient documentClient)
        {
            var databaseId = "providerportal";
            var collectionId = "courses";
            var spId = "ArchiveCoursesForProvider";

            var sprocResourceName = "Dfc.ProviderPortal.TribalExporter.ArchiveCoursesForProvider.js";

            using (var stream = typeof(StoredProcHelper).Assembly.GetManifestResourceStream(sprocResourceName))
            using (var reader = new StreamReader(stream))
            {
                var body = reader.ReadToEnd();

                var sp = new StoredProcedure()
                {
                    Id = spId,
                    Body = body
                };

                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collectionId);
                try
                {
                    await documentClient.CreateStoredProcedureAsync(collectionLink, sp);
                }
                catch (DocumentClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    // Already exists - update it
                    var spLink = UriFactory.CreateStoredProcedureUri(databaseId, collectionId, spId);
                    await documentClient.ReplaceStoredProcedureAsync(spLink, sp);
                }
            }
        }
    }
}

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICosmosDbHelper
    {
        DocumentClient GetClient();

        Task<Database> CreateDatabaseIfNotExistsAsync(DocumentClient client);

        Task<DocumentCollection> CreateDocumentCollectionIfNotExistsAsync(DocumentClient client, string collectionId);

        Task<Document> CreateDocumentAsync(DocumentClient client, string collectionId, object document);

        T DocumentTo<T>(Document document);

        IEnumerable<T> DocumentsTo<T>(IEnumerable<Document> documents);

        Document GetDocumentById<T>(DocumentClient client, string collectionId, T id);
    }
}
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface ICosmosDbHelper
    {
        IDocumentClient GetClient();

        Task<Database> CreateDatabaseIfNotExistsAsync(IDocumentClient client);

        Task<DocumentCollection> CreateDocumentCollectionIfNotExistsAsync(IDocumentClient client, string collectionId);

        Task<Document> CreateDocumentAsync(IDocumentClient client, string collectionId, object document);

        T DocumentTo<T>(Document document);

        IEnumerable<T> DocumentsTo<T>(IEnumerable<Document> documents);

        Document GetDocumentById<T>(IDocumentClient client, string collectionId, T id);
    }
}
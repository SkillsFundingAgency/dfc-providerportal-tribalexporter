function ArchiveCoursesForProvider(ukprn) {
    var collection = getContext().getCollection();
    var collectionLink = collection.getSelfLink();
    var response = getContext().getResponse();

    if (typeof ukprn !== 'number') throw new Error('UKPRN must be a number');

    var updated = 0;

    tryQueryAndUpdate();

    // Recursively queries for a document by id w/ support for continuation tokens.
    // Calls tryUpdate(document) as soon as the query returns a document.
    function tryQueryAndUpdate(continuation) {
        var query = {
            query: "select * from course c where c.ProviderUKPRN = @ukprn and c.CourseStatus <> 4",
            parameters: [
                { name: "@ukprn", value: ukprn }
            ]
        };

        var requestOptions = { continuation: continuation };
        var isAccepted = collection.queryDocuments(collectionLink, query, requestOptions, function (err, documents, responseOptions) {
            if (err) throw err;

            if (documents.length > 0) {
                for (var i = 0; i < documents.length; i++)
                    tryUpdate(documents[i]);
                tryQueryAndUpdate(responseOptions.continuation);
            } else if (responseOptions.continuation) {
                tryQueryAndUpdate(responseOptions.continuation);
            } else {
                response.setBody(updated);
            }
        });
        
        if (!isAccepted) {
            throw new Error("Query failed");
        }
    }
    
    function tryUpdate(document) {
        // DocumentDB supports optimistic concurrency control via HTTP ETag.
        var requestOptions = { etag: document._etag };

        document.CourseStatus = 4;

        document.CourseRuns.forEach(function (ci) {
            ci.RecordStatus = 4;
        });

        // Update the document.
        var isAccepted = collection.replaceDocument(document._self, document, requestOptions, function (err, updatedDocument, responseOptions) {
            if (err) throw err;
            // If we have successfully updated the document - return it in the response body.
            updated++;
        });
        // If we hit execution bounds - throw an exception.
        if (!isAccepted) {
            throw new Error("Update failed.");
        }
    }
}
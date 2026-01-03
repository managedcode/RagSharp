namespace RagSharp.CodeGraph.Core;

public interface IGraphStore : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task SaveIndexAsync(IndexResult result, CancellationToken cancellationToken);
    Task RemoveDocumentsAsync(IEnumerable<string> documentPaths, CancellationToken cancellationToken);
    Task<QueryResult> QueryAsync(QueryRequest request, CancellationToken cancellationToken);
    Task<string> GetSchemaVersionAsync(CancellationToken cancellationToken);
}

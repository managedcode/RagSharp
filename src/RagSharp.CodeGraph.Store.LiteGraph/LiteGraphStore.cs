using Microsoft.Data.Sqlite;
using RagSharp.CodeGraph.Core;

namespace RagSharp.CodeGraph.Store.LiteGraph;

public sealed class LiteGraphStore : IGraphStore
{
    private readonly string _databasePath;
    private SqliteConnection? _connection;

    public LiteGraphStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath) ?? ".");
        _connection = new SqliteConnection($"Data Source={_databasePath}");
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = _connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_version (version TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS nodes (
    id INTEGER PRIMARY KEY,
    kind TEXT NOT NULL,
    name TEXT NOT NULL,
    fullyQualifiedName TEXT,
    documentPath TEXT,
    filePathRelative TEXT,
    startLine INTEGER,
    startColumn INTEGER,
    endLine INTEGER,
    endColumn INTEGER
);
CREATE TABLE IF NOT EXISTS edges (
    id INTEGER PRIMARY KEY,
    kind TEXT NOT NULL,
    sourceId INTEGER NOT NULL,
    targetId INTEGER NOT NULL,
    filePathRelative TEXT,
    startLine INTEGER,
    startColumn INTEGER,
    endLine INTEGER,
    endColumn INTEGER
);
CREATE INDEX IF NOT EXISTS idx_nodes_fqn ON nodes(fullyQualifiedName);
CREATE INDEX IF NOT EXISTS idx_nodes_doc ON nodes(documentPath);
CREATE INDEX IF NOT EXISTS idx_edges_kind ON edges(kind);
";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var version = await GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(version))
        {
            var insert = _connection.CreateCommand();
            insert.CommandText = "INSERT INTO schema_version (version) VALUES ($version);";
            insert.Parameters.AddWithValue("$version", SchemaConstants.CurrentVersion);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveIndexAsync(IndexResult result, CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Store not initialized.");
        }

        using var transaction = _connection.BeginTransaction();
        var clearNodes = _connection.CreateCommand();
        clearNodes.CommandText = "DELETE FROM nodes;";
        await clearNodes.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var clearEdges = _connection.CreateCommand();
        clearEdges.CommandText = "DELETE FROM edges;";
        await clearEdges.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        foreach (var node in result.Nodes)
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"
INSERT INTO nodes (id, kind, name, fullyQualifiedName, documentPath, filePathRelative, startLine, startColumn, endLine, endColumn)
VALUES ($id, $kind, $name, $fqn, $doc, $file, $sl, $sc, $el, $ec);";
            command.Parameters.AddWithValue("$id", node.Id);
            command.Parameters.AddWithValue("$kind", node.Kind.ToString());
            command.Parameters.AddWithValue("$name", node.Name);
            command.Parameters.AddWithValue("$fqn", (object?)node.FullyQualifiedName ?? DBNull.Value);
            command.Parameters.AddWithValue("$doc", (object?)node.DocumentPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$file", (object?)node.Location?.FilePathRelative ?? DBNull.Value);
            command.Parameters.AddWithValue("$sl", (object?)node.Location?.StartLine ?? DBNull.Value);
            command.Parameters.AddWithValue("$sc", (object?)node.Location?.StartColumn ?? DBNull.Value);
            command.Parameters.AddWithValue("$el", (object?)node.Location?.EndLine ?? DBNull.Value);
            command.Parameters.AddWithValue("$ec", (object?)node.Location?.EndColumn ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var edge in result.Edges)
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"
INSERT INTO edges (id, kind, sourceId, targetId, filePathRelative, startLine, startColumn, endLine, endColumn)
VALUES ($id, $kind, $source, $target, $file, $sl, $sc, $el, $ec);";
            command.Parameters.AddWithValue("$id", edge.Id);
            command.Parameters.AddWithValue("$kind", edge.Kind.ToString());
            command.Parameters.AddWithValue("$source", edge.SourceId);
            command.Parameters.AddWithValue("$target", edge.TargetId);
            command.Parameters.AddWithValue("$file", (object?)edge.Location?.FilePathRelative ?? DBNull.Value);
            command.Parameters.AddWithValue("$sl", (object?)edge.Location?.StartLine ?? DBNull.Value);
            command.Parameters.AddWithValue("$sc", (object?)edge.Location?.StartColumn ?? DBNull.Value);
            command.Parameters.AddWithValue("$el", (object?)edge.Location?.EndLine ?? DBNull.Value);
            command.Parameters.AddWithValue("$ec", (object?)edge.Location?.EndColumn ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
    }

    public async Task RemoveDocumentsAsync(IEnumerable<string> documentPaths, CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Store not initialized.");
        }

        foreach (var doc in documentPaths)
        {
            var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM nodes WHERE documentPath = $doc;";
            command.Parameters.AddWithValue("$doc", doc);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<QueryResult> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Store not initialized.");
        }

        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        var nodeCommand = _connection.CreateCommand();
        nodeCommand.CommandText = @"
SELECT id, kind, name, fullyQualifiedName, documentPath, filePathRelative, startLine, startColumn, endLine, endColumn
FROM nodes
WHERE ($kind IS NULL OR kind = $kind)
  AND ($symbol IS NULL OR name LIKE $symbol OR fullyQualifiedName LIKE $symbol)
LIMIT $limit;";
        nodeCommand.Parameters.AddWithValue("$kind", (object?)request.Kind ?? DBNull.Value);
        nodeCommand.Parameters.AddWithValue("$symbol", (object?)request.Symbol is null ? DBNull.Value : $"%{request.Symbol}%");
        nodeCommand.Parameters.AddWithValue("$limit", request.Limit);

        using (var reader = await nodeCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                nodes.Add(ReadNode(reader));
            }
        }

        var edgeCommand = _connection.CreateCommand();
        edgeCommand.CommandText = @"
SELECT id, kind, sourceId, targetId, filePathRelative, startLine, startColumn, endLine, endColumn
FROM edges
WHERE ($symbol IS NULL OR filePathRelative LIKE $symbol)
LIMIT $limit;";
        edgeCommand.Parameters.AddWithValue("$symbol", (object?)request.Document is null ? DBNull.Value : $"%{request.Document}%");
        edgeCommand.Parameters.AddWithValue("$limit", request.Limit);

        using (var reader = await edgeCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                edges.Add(ReadEdge(reader));
            }
        }

        return new QueryResult(SchemaConstants.CurrentVersion, nodes, edges);
    }

    public async Task<string> GetSchemaVersionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            return string.Empty;
        }

        var command = _connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result?.ToString() ?? string.Empty;
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static GraphNode ReadNode(SqliteDataReader reader)
    {
        var location = ReadLocation(reader, 5);
        return new GraphNode(
            reader.GetInt64(0),
            Enum.Parse<NodeKind>(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            location);
    }

    private static GraphEdge ReadEdge(SqliteDataReader reader)
    {
        var location = ReadLocation(reader, 4);
        return new GraphEdge(
            reader.GetInt64(0),
            Enum.Parse<EdgeKind>(reader.GetString(1)),
            reader.GetInt64(2),
            reader.GetInt64(3),
            location);
    }

    private static LocationSpan? ReadLocation(SqliteDataReader reader, int startIndex)
    {
        if (reader.IsDBNull(startIndex))
        {
            return null;
        }

        return new LocationSpan(
            reader.GetString(startIndex),
            reader.GetInt32(startIndex + 1),
            reader.GetInt32(startIndex + 2),
            reader.GetInt32(startIndex + 3),
            reader.GetInt32(startIndex + 4));
    }
}

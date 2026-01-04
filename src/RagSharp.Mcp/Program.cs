using System.Text.Json;
using System.Text.Json.Serialization;
using RagSharp.CodeGraph.Core;
using RagSharp.CodeGraph.Store.LiteGraph;

namespace RagSharp.Mcp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var server = new McpServer();
        await server.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), CancellationToken.None);
        return 0;
    }
}

internal sealed class McpServer
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input);
        await using var writer = new StreamWriter(output) { AutoFlush = true };

        await writer.WriteLineAsync("RagSharp MCP Server v1.0");
        await writer.WriteLineAsync("Awaiting JSON-RPC requests...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            var response = await ProcessRequestAsync(line, cancellationToken);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
    }

    private async Task<JsonRpcResponse> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson, _jsonOptions);
            if (request is null)
            {
                return JsonRpcResponse.Failure(null, -32700, "Parse error");
            }

            return request.Method switch
            {
                "doctor" => await DoctorAsync(request, cancellationToken),
                "index" => await IndexAsync(request, cancellationToken),
                "update" => await UpdateAsync(request, cancellationToken),
                "query" => await QueryAsync(request, cancellationToken),
                "export" => await ExportAsync(request, cancellationToken),
                _ => JsonRpcResponse.Failure(request.Id, -32601, "Method not found")
            };
        }
        catch (JsonException ex)
        {
            return JsonRpcResponse.Failure(null, -32700, $"Parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.Failure(null, -32603, $"Internal error: {ex.Message}");
        }
    }

    private Task<JsonRpcResponse> DoctorAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var root = request.Params?.RootValueOrDefault() ?? Directory.GetCurrentDirectory();
        var result = new { root, status = "ok", message = "MSBuildWorkspace available" };
        return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
    }

    private async Task<JsonRpcResponse> IndexAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var root = request.Params?.RootValueOrDefault() ?? Directory.GetCurrentDirectory();
        var dbPath = request.Params?.DbValueOrDefault() ?? Path.Combine(root, ".ragsharp", "graph", "index.db");
        var statePath = request.Params?.StateValueOrDefault() ?? Path.Combine(root, ".ragsharp", "graph", "state.json");
        var includeDataflow = request.Params?.GetValueOrDefault("includeDataflow", false) ?? false;

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(cancellationToken);

        var indexer = new CodeGraphIndexer(includeDataflow);
        var state = await CodeGraphIndexer.LoadStateAsync(statePath, cancellationToken);
        var currentHashes = CodeGraphIndexer.ComputeFileHashes(root);

        var indexResult = await indexer.IndexAsync(root, cancellationToken);
        await store.SaveIndexAsync(indexResult, cancellationToken);
        WriteSchemaVersionFile(root);
        await CodeGraphIndexer.SaveStateAsync(statePath, new IndexState(currentHashes), cancellationToken);

        var result = new { status = "indexed", nodes = indexResult.Nodes.Count, edges = indexResult.Edges.Count };
        return JsonRpcResponse.Success(request.Id, result);
    }

    private async Task<JsonRpcResponse> UpdateAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var root = request.Params?.RootValueOrDefault() ?? Directory.GetCurrentDirectory();
        var dbPath = request.Params?.DbValueOrDefault() ?? Path.Combine(root, ".ragsharp", "graph", "index.db");
        var statePath = request.Params?.StateValueOrDefault() ?? Path.Combine(root, ".ragsharp", "graph", "state.json");
        var includeDataflow = request.Params?.GetValueOrDefault("includeDataflow", false) ?? false;

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(cancellationToken);

        var indexer = new CodeGraphIndexer(includeDataflow);
        var state = await CodeGraphIndexer.LoadStateAsync(statePath, cancellationToken);
        var currentHashes = CodeGraphIndexer.ComputeFileHashes(root);

        var diff = CodeGraphIndexer.ComputeDiff(state, currentHashes);
        if (diff.RemovedFiles.Count > 0)
        {
            await store.RemoveDocumentsAsync(diff.RemovedFiles, cancellationToken);
        }

        var indexResult = await indexer.IndexAsync(root, cancellationToken);
        await store.SaveIndexAsync(indexResult, cancellationToken);
        WriteSchemaVersionFile(root);
        await CodeGraphIndexer.SaveStateAsync(statePath, new IndexState(currentHashes), cancellationToken);

        var result = new { status = "updated", changed = diff.ChangedFiles.Count, removed = diff.RemovedFiles.Count, nodes = indexResult.Nodes.Count, edges = indexResult.Edges.Count };
        return JsonRpcResponse.Success(request.Id, result);
    }

    private async Task<JsonRpcResponse> QueryAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var dbPath = request.Params?.DbValueOrDefault() ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "index.db");
        var queryType = request.Params?.GetValueOrDefault("type", "symbols") ?? "symbols";
        var symbol = request.Params?.GetValueOrDefault<string?>("symbol", null);
        var kind = request.Params?.GetValueOrDefault<string?>("kind", null);
        var document = request.Params?.GetValueOrDefault<string?>("document", null);
        var edgeKind = request.Params?.GetValueOrDefault<string?>("edgeKind", null);
        var limit = request.Params?.GetValueOrDefault("limit", 100) ?? 100;
        var contextLines = request.Params?.GetValueOrDefault("contextLines", 2) ?? 2;

        if (!File.Exists(dbPath))
        {
            return JsonRpcResponse.Failure(request.Id, -32000, "Index database not found");
        }

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(cancellationToken);

        var queryRequest = new QueryRequest(queryType, symbol, kind, document, edgeKind, limit, contextLines);
        var result = await store.QueryAsync(queryRequest, cancellationToken);

        return JsonRpcResponse.Success(request.Id, result);
    }

    private async Task<JsonRpcResponse> ExportAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var dbPath = request.Params?.DbValueOrDefault() ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "index.db");
        var format = request.Params?.GetValueOrDefault("format", "dot") ?? "dot";
        var output = request.Params?.GetValueOrDefault("output", Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "graph.dot")) ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "graph.dot");

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(cancellationToken);
        var queryResult = await store.QueryAsync(new QueryRequest("export", null, null, null, null, 10000, 0), cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        if (format.Equals("gexf", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(output, GexfExporter.Export(queryResult), cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(output, DotExporter.Export(queryResult), cancellationToken);
        }

        var result = new { status = "exported", path = output, format };
        return JsonRpcResponse.Success(request.Id, result);
    }

    private static void WriteSchemaVersionFile(string root)
    {
        var schemaPath = Path.Combine(root, ".ragsharp", "graph", "schema_version");
        Directory.CreateDirectory(Path.GetDirectoryName(schemaPath) ?? ".");
        File.WriteAllText(schemaPath, SchemaConstants.CurrentVersion);
    }
}

internal sealed record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] object? Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] Dictionary<string, JsonElement>? Params);

internal sealed record JsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] object? Id,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] JsonRpcError? Error)
{
    public static JsonRpcResponse Success(object? id, object? result) => new("2.0", id, result, null);
    public static JsonRpcResponse Failure(object? id, int code, string message) => new("2.0", id, null, new JsonRpcError(code, message));
}

internal sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message);

internal static class JsonParamsExtensions
{
    public static string? RootValueOrDefault(this Dictionary<string, JsonElement>? @params) =>
        @params?.GetValueOrDefault<string?>("root", null);

    public static string? DbValueOrDefault(this Dictionary<string, JsonElement>? @params) =>
        @params?.GetValueOrDefault<string?>("db", null);

    public static string? StateValueOrDefault(this Dictionary<string, JsonElement>? @params) =>
        @params?.GetValueOrDefault<string?>("state", null);

    public static T? GetValueOrDefault<T>(this Dictionary<string, JsonElement>? @params, string key, T? defaultValue)
    {
        if (@params is null || !@params.TryGetValue(key, out var element))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }
}

internal static class DotExporter
{
    public static string Export(QueryResult result)
    {
        var lines = new List<string> { "digraph G {" };
        foreach (var node in result.Nodes)
        {
            lines.Add($"  {node.Id} [label=\"{node.Kind}:{node.Name}\"]; ");
        }

        foreach (var edge in result.Edges)
        {
            lines.Add($"  {edge.SourceId} -> {edge.TargetId} [label=\"{edge.Kind}\"]; ");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }
}

internal static class GexfExporter
{
    public static string Export(QueryResult result)
    {
        var lines = new List<string>
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">",
            "<graph mode=\"static\" defaultedgetype=\"directed\">",
            "<nodes>"
        };

        foreach (var node in result.Nodes)
        {
            lines.Add($"<node id=\"{node.Id}\" label=\"{node.Kind}:{node.Name}\" />");
        }

        lines.Add("</nodes>");
        lines.Add("<edges>");

        foreach (var edge in result.Edges)
        {
            lines.Add($"<edge id=\"{edge.Id}\" source=\"{edge.SourceId}\" target=\"{edge.TargetId}\" label=\"{edge.Kind}\" />");
        }

        lines.Add("</edges>");
        lines.Add("</graph>");
        lines.Add("</gexf>");
        return string.Join(Environment.NewLine, lines);
    }
}

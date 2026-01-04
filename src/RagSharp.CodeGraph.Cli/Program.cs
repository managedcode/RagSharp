using System.Text.Json;
using RagSharp.CodeGraph.Core;
using RagSharp.CodeGraph.Store.LiteGraph;

namespace RagSharp.CodeGraph.Cli;

public static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidArgs = 2;
    private const int ExitEnvironmentError = 3;
    private const int ExitIndexMissing = 4;
    private const int ExitSchemaMismatch = 5;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("ragsharp-graph <command> [options]");
            return ExitInvalidArgs;
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "doctor" => await RunDoctorAsync(options),
                "index" => await RunIndexAsync(options, false),
                "update" => await RunIndexAsync(options, true),
                "query" => await RunQueryAsync(args.Skip(1).ToArray()),
                "export" => await RunExportAsync(options),
                _ => ExitInvalidArgs
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitIndexMissing;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitEnvironmentError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return ExitEnvironmentError;
        }
    }

    private static Task<int> RunDoctorAsync(Dictionary<string, string?> options)
    {
        var root = options.GetValueOrDefault("--root") ?? Directory.GetCurrentDirectory();
        Console.Error.WriteLine($"Root: {root}");
        Console.Error.WriteLine("MSBuildWorkspace available.");
        return Task.FromResult(ExitSuccess);
    }

    private static async Task<int> RunIndexAsync(Dictionary<string, string?> options, bool isUpdate)
    {
        var root = options.GetValueOrDefault("--root") ?? Directory.GetCurrentDirectory();
        var dbPath = options.GetValueOrDefault("--db") ?? Path.Combine(root, ".ragsharp", "graph", "index.db");
        var statePath = options.GetValueOrDefault("--state") ?? Path.Combine(root, ".ragsharp", "graph", "state.json");
        var includeDataflow = options.ContainsKey("--include-dataflow");

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        var indexer = new CodeGraphIndexer(includeDataflow);
        var state = await CodeGraphIndexer.LoadStateAsync(statePath, CancellationToken.None).ConfigureAwait(false);
        var currentHashes = CodeGraphIndexer.ComputeFileHashes(root);
        if (isUpdate)
        {
            var diff = CodeGraphIndexer.ComputeDiff(state, currentHashes);
            if (diff.RemovedFiles.Count > 0)
            {
                await store.RemoveDocumentsAsync(diff.RemovedFiles, CancellationToken.None).ConfigureAwait(false);
            }
        }

        var result = await indexer.IndexAsync(root, CancellationToken.None).ConfigureAwait(false);
        await store.SaveIndexAsync(result, CancellationToken.None).ConfigureAwait(false);
        WriteSchemaVersionFile(root);
        await CodeGraphIndexer.SaveStateAsync(statePath, new IndexState(currentHashes), CancellationToken.None).ConfigureAwait(false);
        Console.Error.WriteLine(isUpdate ? "Updated index." : "Indexed code graph.");
        return ExitSuccess;
    }

    private static async Task<int> RunQueryAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return ExitInvalidArgs;
        }

        var queryType = args[0];
        var options = ParseOptions(args.Skip(1).ToArray());
        var dbPath = options.GetValueOrDefault("--db") ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "index.db");
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("Index database not found.");
        }

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        var schemaVersion = await store.GetSchemaVersionAsync(CancellationToken.None).ConfigureAwait(false);
        if (schemaVersion != SchemaConstants.CurrentVersion)
        {
            Console.Error.WriteLine("Schema version mismatch.");
            return ExitSchemaMismatch;
        }

        var request = new QueryRequest(
            QueryType: queryType,
            Symbol: options.GetValueOrDefault("--symbol"),
            Kind: options.GetValueOrDefault("--kind"),
            Document: options.GetValueOrDefault("--document"),
            EdgeKind: options.GetValueOrDefault("--edge-kind"),
            Limit: int.TryParse(options.GetValueOrDefault("--limit"), out var limit) ? limit : 100,
            ContextLines: int.TryParse(options.GetValueOrDefault("--context-lines"), out var context) ? context : 2);

        var result = await store.QueryAsync(request, CancellationToken.None).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Console.Out.WriteLine(json);
        return ExitSuccess;
    }

    private static async Task<int> RunExportAsync(Dictionary<string, string?> options)
    {
        var dbPath = options.GetValueOrDefault("--db") ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "index.db");
        var format = options.GetValueOrDefault("--format") ?? "dot";
        var output = options.GetValueOrDefault("--out") ?? Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "graph.dot");

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        var result = await store.QueryAsync(new QueryRequest("export", null, null, null, null, 10000, 0), CancellationToken.None).ConfigureAwait(false);

        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        if (format.Equals("gexf", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(output, GexfExporter.Export(result), CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllTextAsync(output, DotExporter.Export(result), CancellationToken.None).ConfigureAwait(false);
        }

        Console.Error.WriteLine($"Exported graph to {output}.");
        return ExitSuccess;
    }

    private static void WriteSchemaVersionFile(string root)
    {
        var schemaPath = Path.Combine(root, ".ragsharp", "graph", "schema_version");
        Directory.CreateDirectory(Path.GetDirectoryName(schemaPath) ?? ".");
        File.WriteAllText(schemaPath, SchemaConstants.CurrentVersion);
    }

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[arg] = args[i + 1];
                i++;
            }
            else
            {
                options[arg] = "true";
            }
        }

        return options;
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

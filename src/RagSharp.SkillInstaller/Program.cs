using System.CommandLine;
using System.Text.Json;
using RagSharp.CodeGraph.Core;
using RagSharp.CodeGraph.Store.LiteGraph;

namespace RagSharp.SkillInstaller;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Shared options
        var rootOption = new Option<string>("--root", () => Directory.GetCurrentDirectory(), "Repository root");
        var skillDirOption = new Option<string>("--skill-dir", () => ".codex/skills", "Skill directory");
        var forceOption = new Option<bool>("--force", "Force install");
        var verboseOption = new Option<bool>("--verbose", "Verbose output");

        // Installer commands
        var installCommand = new Command("install", "Install ragsharp skills");
        installCommand.AddOption(rootOption);
        installCommand.AddOption(skillDirOption);
        installCommand.AddOption(forceOption);
        installCommand.AddOption(verboseOption);
        installCommand.SetHandler(async (root, skillDir, force, verbose) =>
        {
            await Installer.InstallAsync(root, skillDir, force, verbose, CancellationToken.None).ConfigureAwait(false);
        }, rootOption, skillDirOption, forceOption, verboseOption);

        var uninstallCommand = new Command("uninstall", "Uninstall ragsharp skills");
        uninstallCommand.AddOption(rootOption);
        uninstallCommand.AddOption(skillDirOption);
        uninstallCommand.SetHandler(async (root, skillDir) =>
        {
            await Installer.UninstallAsync(root, skillDir, CancellationToken.None).ConfigureAwait(false);
        }, rootOption, skillDirOption);

        var statusCommand = new Command("status", "Show install status");
        var formatOption = new Option<string>("--format", () => "json", "Output format");
        statusCommand.AddOption(rootOption);
        statusCommand.AddOption(skillDirOption);
        statusCommand.AddOption(formatOption);
        statusCommand.SetHandler(async (root, skillDir, format) =>
        {
            var status = await Installer.GetStatusAsync(root, skillDir, CancellationToken.None).ConfigureAwait(false);
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            }
            else
            {
                Console.Out.WriteLine(status.Installed ? "installed" : "not installed");
            }
        }, rootOption, skillDirOption, formatOption);

        // Graph options
        var dbOption = new Option<string>("--db", () => Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "index.db"), "Path to graph database");
        var stateOption = new Option<string>("--state", () => Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "state.json"), "Path to index state file");
        var includeDataflowOption = new Option<bool>("--include-dataflow", "Include dataflow analysis (slower)");
        var queryTypeOption = new Option<string>("--type", () => "symbols", "Query type (e.g., symbols, usings, references)");
        var symbolOption = new Option<string?>("--symbol", description: "Symbol filter");
        var kindOption = new Option<string?>("--kind", description: "Node kind filter");
        var documentOption = new Option<string?>("--document", description: "Document path filter");
        var edgeKindOption = new Option<string?>("--edge-kind", description: "Edge kind filter");
        var limitOption = new Option<int>("--limit", () => 100, "Result limit");
        var contextLinesOption = new Option<int>("--context-lines", () => 2, "Context lines for queries");
        var formatOptionExport = new Option<string>("--format", () => "dot", "Export format: dot|gexf");
        var outputOption = new Option<string>("--out", () => Path.Combine(Directory.GetCurrentDirectory(), ".ragsharp", "graph", "graph.dot"), "Output path for export");

        // Graph commands
        var graphDoctorCommand = new Command("doctor", "Check graph environment");
        graphDoctorCommand.AddOption(rootOption);
        graphDoctorCommand.SetHandler((root) =>
        {
            GraphCommands.RunDoctor(root);
        }, rootOption);

        var graphIndexCommand = new Command("index", "Build a fresh code graph index");
        graphIndexCommand.AddOption(rootOption);
        graphIndexCommand.AddOption(dbOption);
        graphIndexCommand.AddOption(stateOption);
        graphIndexCommand.AddOption(includeDataflowOption);
        graphIndexCommand.SetHandler(async (root, db, state, includeDataflow) =>
        {
            await GraphCommands.RunIndexAsync(root, db, state, includeDataflow, isUpdate: false).ConfigureAwait(false);
        }, rootOption, dbOption, stateOption, includeDataflowOption);

        var graphUpdateCommand = new Command("update", "Incrementally update the code graph index");
        graphUpdateCommand.AddOption(rootOption);
        graphUpdateCommand.AddOption(dbOption);
        graphUpdateCommand.AddOption(stateOption);
        graphUpdateCommand.AddOption(includeDataflowOption);
        graphUpdateCommand.SetHandler(async (root, db, state, includeDataflow) =>
        {
            await GraphCommands.RunIndexAsync(root, db, state, includeDataflow, isUpdate: true).ConfigureAwait(false);
        }, rootOption, dbOption, stateOption, includeDataflowOption);

        var graphQueryCommand = new Command("query", "Query the code graph");
        graphQueryCommand.AddOption(dbOption);
        graphQueryCommand.AddOption(queryTypeOption);
        graphQueryCommand.AddOption(symbolOption);
        graphQueryCommand.AddOption(kindOption);
        graphQueryCommand.AddOption(documentOption);
        graphQueryCommand.AddOption(edgeKindOption);
        graphQueryCommand.AddOption(limitOption);
        graphQueryCommand.AddOption(contextLinesOption);
        graphQueryCommand.SetHandler(async (db, type, symbol, kind, document, edgeKind, limit, contextLines) =>
        {
            await GraphCommands.RunQueryAsync(db, type, symbol, kind, document, edgeKind, limit, contextLines).ConfigureAwait(false);
        }, dbOption, queryTypeOption, symbolOption, kindOption, documentOption, edgeKindOption, limitOption, contextLinesOption);

        var graphExportCommand = new Command("export", "Export the code graph");
        graphExportCommand.AddOption(dbOption);
        graphExportCommand.AddOption(formatOptionExport);
        graphExportCommand.AddOption(outputOption);
        graphExportCommand.SetHandler(async (db, format, output) =>
        {
            await GraphCommands.RunExportAsync(db, format, output).ConfigureAwait(false);
        }, dbOption, formatOptionExport, outputOption);

        var graphCommand = new Command("graph", "Code graph operations")
        {
            graphDoctorCommand,
            graphIndexCommand,
            graphUpdateCommand,
            graphQueryCommand,
            graphExportCommand
        };

        var rootCommand = new RootCommand("ragsharp")
        {
            installCommand,
            uninstallCommand,
            statusCommand,
            graphCommand,
            // Back-compat doctor (acts on graph environment)
            graphDoctorCommand
        };

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}

internal static class GraphCommands
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidArgs = 2;
    private const int ExitEnvironmentError = 3;
    private const int ExitIndexMissing = 4;
    private const int ExitSchemaMismatch = 5;

    public static void RunDoctor(string root)
    {
        var rootPath = ResolveRoot(root);
        Console.Error.WriteLine($"Root: {rootPath}");
        Console.Error.WriteLine("MSBuildWorkspace available.");
    }

    public static async Task<int> RunIndexAsync(string root, string dbPath, string statePath, bool includeDataflow, bool isUpdate)
    {
        try
        {
            var rootPath = ResolveRoot(root);
            var store = new LiteGraphStore(dbPath);
            await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

            var indexer = new CodeGraphIndexer(includeDataflow);
            var state = await CodeGraphIndexer.LoadStateAsync(statePath, CancellationToken.None).ConfigureAwait(false);
            var currentHashes = CodeGraphIndexer.ComputeFileHashes(rootPath);
            if (isUpdate)
            {
                var diff = CodeGraphIndexer.ComputeDiff(state, currentHashes);
                if (diff.RemovedFiles.Count > 0)
                {
                    await store.RemoveDocumentsAsync(diff.RemovedFiles, CancellationToken.None).ConfigureAwait(false);
                }
            }

            var result = await indexer.IndexAsync(rootPath, CancellationToken.None).ConfigureAwait(false);
            await store.SaveIndexAsync(result, CancellationToken.None).ConfigureAwait(false);
            WriteSchemaVersionFile(rootPath);
            await CodeGraphIndexer.SaveStateAsync(statePath, new IndexState(currentHashes), CancellationToken.None).ConfigureAwait(false);
            Console.Error.WriteLine(isUpdate ? "Updated index." : "Indexed code graph.");
            return ExitSuccess;
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

    public static async Task<int> RunQueryAsync(string dbPath, string queryType, string? symbol, string? kind, string? document, string? edgeKind, int limit, int contextLines)
    {
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("Index database not found.");
            return ExitIndexMissing;
        }

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        var schemaVersion = await store.GetSchemaVersionAsync(CancellationToken.None).ConfigureAwait(false);
        if (!string.Equals(schemaVersion, SchemaConstants.CurrentVersion, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Schema version mismatch.");
            return ExitSchemaMismatch;
        }

        var request = new QueryRequest(
            QueryType: queryType,
            Symbol: symbol,
            Kind: kind,
            Document: document,
            EdgeKind: edgeKind,
            Limit: limit,
            ContextLines: contextLines);

        var result = await store.QueryAsync(request, CancellationToken.None).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Console.Out.WriteLine(json);
        return ExitSuccess;
    }

    public static async Task<int> RunExportAsync(string dbPath, string format, string output)
    {
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

    private static string ResolveRoot(string root)
    {
        var current = Path.GetFullPath(root);
        var dir = new DirectoryInfo(current);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return current;
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

using RagSharp.CodeGraph.Core;
using RagSharp.CodeGraph.Store.LiteGraph;

namespace RagSharp.CodeGraph.Tests;

public sealed class CodeGraphIntegrationTests
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    [Test]
    [MethodDataSource(nameof(ClassNameData))]
    public async Task QueryFindsGeneratedTypes(string className)
    {
        var context = await GetContextAsync();
        var query = await context.Store.QueryAsync(
            new QueryRequest("symbols", className, NodeKind.Type.ToString(), null, 5, 0),
            CancellationToken.None);

        await Assert.That(query.Nodes.Any(node => node.Name == className)).IsTrue();
    }

    [Test]
    public async Task GraphIncludesBaseTypeEdges()
    {
        var context = await GetContextAsync();

        var derivedIds = context.Result.Nodes
            .Where(node => node.Name == "GeneratedClass010")
            .Select(node => node.Id)
            .ToHashSet();
        var baseIds = context.Result.Nodes
            .Where(node => node.Name == "BaseType")
            .Select(node => node.Id)
            .ToHashSet();

        var inherits = context.Result.Edges.Any(edge =>
            edge.Kind == EdgeKind.Inherits &&
            derivedIds.Contains(edge.SourceId) &&
            baseIds.Contains(edge.TargetId));

        await Assert.That(inherits).IsTrue();
    }

    [Test]
    public async Task GraphCapturesUsingDirectives()
    {
        var context = await GetContextAsync();
        var query = await context.Store.QueryAsync(
            new QueryRequest("usings", "System", NodeKind.UsingDirective.ToString(), null, 5, 0),
            CancellationToken.None);

        await Assert.That(query.Nodes.Any(node => node.Name == "System")).IsTrue();
    }

    [Test]
    public async Task ComputeDiffReportsChangedAndRemovedFiles()
    {
        var context = await GetContextAsync();
        var previous = new IndexState(context.FileHashes);

        var updated = new Dictionary<string, string>(context.FileHashes, StringComparer.OrdinalIgnoreCase);
        var key = updated.Keys.First();
        updated[key] = "DIFFERENT";
        updated.Remove(updated.Keys.Skip(1).First());

        var diff = CodeGraphIndexer.ComputeDiff(previous, updated);

        await Assert.That(diff.ChangedFiles).Contains(key);
        await Assert.That(diff.RemovedFiles).IsNotEmpty();
    }

    private static async Task<IndexTestContext> GetContextAsync()
    {
        if (IndexTestContextHolder.Context is not null)
        {
            return IndexTestContextHolder.Context;
        }

        await InitLock.WaitAsync();
        try
        {
            IndexTestContextHolder.Context ??= await IndexTestContext.CreateAsync();
        }
        finally
        {
            InitLock.Release();
        }

        return IndexTestContextHolder.Context;
    }

    public static IEnumerable<string> ClassNameData()
    {
        for (var i = 1; i <= 100; i += 1)
        {
            yield return $"GeneratedClass{i:D3}";
        }
    }
}

internal sealed class IndexTestContext : IAsyncDisposable
{
    private IndexTestContext(string rootPath, LiteGraphStore store, IndexResult result, IReadOnlyDictionary<string, string> fileHashes)
    {
        RootPath = rootPath;
        Store = store;
        Result = result;
        FileHashes = fileHashes;
    }

    public string RootPath { get; }
    public LiteGraphStore Store { get; }
    public IndexResult Result { get; }
    public IReadOnlyDictionary<string, string> FileHashes { get; }

    public static async Task<IndexTestContext> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ragsharp-codegraph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var projectDir = Path.Combine(root, "GeneratedProject");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, ".git"));

        var projectPath = Path.Combine(projectDir, "GeneratedProject.csproj");
        await File.WriteAllTextAsync(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

        await File.WriteAllTextAsync(Path.Combine(projectDir, "BaseTypes.cs"), """
namespace GeneratedProject;

public interface ITagged
{
    string Tag { get; }
}

public abstract class BaseType
{
    public abstract string GetId();
}
""");

        for (var i = 1; i <= 100; i += 1)
        {
            var name = $"GeneratedClass{i:D3}";
            var inherits = i % 10 == 0;
            var code = inherits
                ? $$"""
namespace GeneratedProject;

public sealed class {{name}} : BaseType, ITagged
{
    public override string GetId() => "{{name}}";
    public string Tag => "{{name}}";
}
"""
                : $$"""
namespace GeneratedProject;

public sealed class {{name}}
{
    public string Tag => "{{name}}";
}
""";

            await File.WriteAllTextAsync(Path.Combine(projectDir, $"{name}.cs"), code);
        }

        var indexer = new CodeGraphIndexer(includeDataflow: false);
        var result = await indexer.IndexAsync(projectDir, CancellationToken.None);

        var dbPath = Path.Combine(projectDir, ".codegraph", "index.db");
        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveIndexAsync(result, CancellationToken.None);

        var hashes = CodeGraphIndexer.ComputeFileHashes(projectDir);
        return new IndexTestContext(root, store, result, hashes);
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, true);
        }
    }
}

internal static class IndexTestContextHolder
{
    public static IndexTestContext? Context { get; set; }
}

public sealed class CodeGraphTestHooks
{
    [After(TestSession)]
    public static async Task Cleanup()
    {
        if (IndexTestContextHolder.Context is { } context)
        {
            await context.DisposeAsync();
            IndexTestContextHolder.Context = null;
        }
    }
}

using RagSharp.CodeGraph.Core;
using RagSharp.CodeGraph.Store.LiteGraph;
using Xunit;

namespace RagSharp.CodeGraph.Tests;

public class IndexTests
{
    [Fact]
    public async Task IndexesSampleSolution()
    {
        var root = Path.GetFullPath(Path.Combine("..", "..", "..", "samples", "SampleApp"));
        var dbPath = Path.Combine(root, ".codegraph", "index.db");
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        var indexer = new CodeGraphIndexer(includeDataflow: false);
        var result = await indexer.IndexAsync(root, CancellationToken.None);

        var store = new LiteGraphStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveIndexAsync(result, CancellationToken.None);

        var query = await store.QueryAsync(new QueryRequest("symbols", "Greeter", null, null, 10, 0), CancellationToken.None);
        Assert.Contains(query.Nodes, node => node.Name == "Greeter");
    }
}

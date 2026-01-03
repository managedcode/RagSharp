using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace RagSharp.CodeGraph.Core;

public sealed class CodeGraphIndexer
{
    private readonly bool _includeDataflow;

    public CodeGraphIndexer(bool includeDataflow)
    {
        _includeDataflow = includeDataflow;
    }

    public async Task<IndexResult> IndexAsync(string rootPath, CancellationToken cancellationToken)
    {
        MSBuildLocator.RegisterDefaults();
        using var workspace = MSBuildWorkspace.Create();

        var solutionPath = FindSolution(rootPath);
        if (solutionPath is not null)
        {
            var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken).ConfigureAwait(false);
            return await BuildIndexAsync(rootPath, solution.Projects, cancellationToken).ConfigureAwait(false);
        }

        var projectPath = FindProject(rootPath);
        if (projectPath is null)
        {
            throw new InvalidOperationException("No .sln or .csproj found under the provided root.");
        }

        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken).ConfigureAwait(false);
        return await BuildIndexAsync(rootPath, new[] { project }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IndexState> LoadStateAsync(string statePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath))
        {
            return new IndexState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var json = await File.ReadAllTextAsync(statePath, cancellationToken).ConfigureAwait(false);
        return IndexState.FromJson(json);
    }

    public static Task SaveStateAsync(string statePath, IndexState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath) ?? ".");
        return File.WriteAllTextAsync(statePath, state.ToJson(), cancellationToken);
    }

    public static IndexDiff ComputeDiff(IndexState previous, IReadOnlyDictionary<string, string> current)
    {
        var changed = new List<string>();
        var removed = new List<string>();

        foreach (var (path, hash) in current)
        {
            if (!previous.Files.TryGetValue(path, out var existing) || !string.Equals(existing, hash, StringComparison.Ordinal))
            {
                changed.Add(path);
            }
        }

        foreach (var path in previous.Files.Keys)
        {
            if (!current.ContainsKey(path))
            {
                removed.Add(path);
            }
        }

        return new IndexDiff(changed, removed);
    }

    private async Task<IndexResult> BuildIndexAsync(string rootPath, IEnumerable<Project> projects, CancellationToken cancellationToken)
    {
        var builder = new GraphBuilder(rootPath, _includeDataflow);
        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            builder.AddProject(project);

            foreach (var document in project.Documents)
            {
                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxTree is null)
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                builder.AddDocument(project, document, syntaxTree, semanticModel);
            }
        }

        return builder.Build();
    }

    public static IReadOnlyDictionary<string, string> ComputeFileHashes(string rootPath)
    {
        var files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(rootPath, file);
            using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            result[relative] = hash;
        }

        return result;
    }

    private static string? FindSolution(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static string? FindProject(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
    }
}

public sealed record IndexResult(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record IndexDiff(
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> RemovedFiles);

public sealed class IndexState
{
    public IReadOnlyDictionary<string, string> Files { get; }

    public IndexState(IReadOnlyDictionary<string, string> files)
    {
        Files = files;
    }

    public string ToJson()
    {
        var payload = new IndexStatePayload { Files = new Dictionary<string, string>(Files) };
        return System.Text.Json.JsonSerializer.Serialize(payload, new() { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
    }

    public static IndexState FromJson(string json)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<IndexStatePayload>(json) ?? new IndexStatePayload();
        return new IndexState(payload.Files);
    }

    private sealed class IndexStatePayload
    {
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed class GraphBuilder
{
    private readonly string _rootPath;
    private readonly bool _includeDataflow;
    private readonly List<GraphNode> _nodes = new();
    private readonly List<GraphEdge> _edges = new();
    private long _nextNodeId = 1;
    private long _nextEdgeId = 1;
    private readonly Dictionary<string, long> _documentNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly long _solutionNodeId;

    public GraphBuilder(string rootPath, bool includeDataflow)
    {
        _rootPath = rootPath;
        _includeDataflow = includeDataflow;
        _solutionNodeId = AddNode(NodeKind.Solution, Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar)), rootPath, null);
    }

    public void AddProject(Project project)
    {
        var nodeId = AddNode(NodeKind.Project, project.Name, project.FilePath, null);
        AddEdge(EdgeKind.Contains, _solutionNodeId, nodeId, null);
        if (project.FilePath is { } filePath)
        {
            var relative = Path.GetRelativePath(_rootPath, filePath);
            _documentNodes[relative] = nodeId;
        }
    }

    public void AddDocument(Project project, Document document, SyntaxTree tree, SemanticModel semanticModel)
    {
        if (document.FilePath is null)
        {
            return;
        }

        var relativePath = Path.GetRelativePath(_rootPath, document.FilePath);
        var documentNodeId = AddNode(NodeKind.Document, document.Name, document.FilePath, null);
        _documentNodes[relativePath] = documentNodeId;
        AddEdge(EdgeKind.Contains, _solutionNodeId, documentNodeId, null);

        var root = tree.GetRoot();
        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name.ToString();
            var nodeId = AddNode(NodeKind.UsingDirective, name, document.FilePath, GetLocation(_rootPath, usingDirective));
            AddEdge(EdgeKind.UsingDirective, documentNodeId, nodeId, GetLocation(_rootPath, usingDirective));
        }

        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
            var name = symbol?.Name ?? typeDecl.Identifier.Text;
            var fq = symbol?.ToDisplayString();
            var typeNodeId = AddNode(NodeKind.Type, name, document.FilePath, GetLocation(_rootPath, typeDecl), fq);
            AddEdge(EdgeKind.DeclaredAt, documentNodeId, typeNodeId, GetLocation(_rootPath, typeDecl));

            if (symbol is INamedTypeSymbol namedType)
            {
                if (namedType.BaseType is { } baseType && baseType.SpecialType != SpecialType.System_Object)
                {
                    var baseNodeId = AddNode(NodeKind.Type, baseType.Name, document.FilePath, null, baseType.ToDisplayString());
                    AddEdge(EdgeKind.Inherits, typeNodeId, baseNodeId, GetLocation(_rootPath, typeDecl));
                }

                foreach (var iface in namedType.Interfaces)
                {
                    var ifaceNodeId = AddNode(NodeKind.Type, iface.Name, document.FilePath, null, iface.ToDisplayString());
                    AddEdge(EdgeKind.Implements, typeNodeId, ifaceNodeId, GetLocation(_rootPath, typeDecl));
                }
            }
        }

        foreach (var memberDecl in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (memberDecl is BaseTypeDeclarationSyntax)
            {
                continue;
            }

            var symbol = semanticModel.GetDeclaredSymbol(memberDecl);
            if (symbol is null)
            {
                continue;
            }

            var name = symbol.Name;
            var fq = symbol.ToDisplayString();
            var memberNodeId = AddNode(NodeKind.Member, name, document.FilePath, GetLocation(_rootPath, memberDecl), fq);
            AddEdge(EdgeKind.DeclaredAt, documentNodeId, memberNodeId, GetLocation(_rootPath, memberDecl));
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol is null)
            {
                continue;
            }

            var targetNodeId = AddNode(NodeKind.Member, symbol.Name, document.FilePath, null, symbol.ToDisplayString());
            AddEdge(EdgeKind.MethodInvocation, documentNodeId, targetNodeId, GetLocation(_rootPath, invocation));
        }

        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
            if (symbol is null)
            {
                continue;
            }

            var attrNodeId = AddNode(NodeKind.Type, symbol.Name, document.FilePath, null, symbol.ToDisplayString());
            AddEdge(EdgeKind.AttributeUsage, documentNodeId, attrNodeId, GetLocation(_rootPath, attribute));
        }

        if (_includeDataflow)
        {
            foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                var bodyNode = (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;
                if (bodyNode is null)
                {
                    continue;
                }

                var dataFlow = semanticModel.AnalyzeDataFlow(bodyNode);
                if (dataFlow is null)
                {
                    continue;
                }

                foreach (var symbol in dataFlow.VariablesDeclared)
                {
                    var variableNodeId = AddNode(NodeKind.LocalVariable, symbol.Name, document.FilePath, null, symbol.ToDisplayString());
                    AddEdge(EdgeKind.DefinedAt, documentNodeId, variableNodeId, GetLocation(_rootPath, method));
                }

                foreach (var symbol in dataFlow.ReadInside)
                {
                    var variableNodeId = AddNode(NodeKind.LocalVariable, symbol.Name, document.FilePath, null, symbol.ToDisplayString());
                    AddEdge(EdgeKind.UsedAt, documentNodeId, variableNodeId, GetLocation(_rootPath, method));
                }
            }
        }
    }

    public IndexResult Build() => new(_nodes, _edges);

    private long AddNode(NodeKind kind, string name, string? documentPath, LocationSpan? location, string? fullyQualifiedName = null)
    {
        var node = new GraphNode(_nextNodeId++, kind, name, fullyQualifiedName, documentPath is null ? null : Path.GetRelativePath(_rootPath, documentPath), location);
        _nodes.Add(node);
        return node.Id;
    }

    private void AddEdge(EdgeKind kind, long sourceId, long targetId, LocationSpan? location)
    {
        _edges.Add(new GraphEdge(_nextEdgeId++, kind, sourceId, targetId, location));
    }

    private static LocationSpan? GetLocation(string rootPath, SyntaxNode node)
    {
        var location = node.GetLocation();
        var span = location.GetLineSpan();
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;
        var relativePath = string.IsNullOrEmpty(span.Path) ? span.Path : Path.GetRelativePath(rootPath, span.Path);
        return new LocationSpan(
            FilePathRelative: relativePath,
            StartLine: start.Line + 1,
            StartColumn: start.Character + 1,
            EndLine: end.Line + 1,
            EndColumn: end.Character + 1);
    }
}

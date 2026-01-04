using System.Text.Json.Serialization;

namespace RagSharp.CodeGraph.Core;

public enum NodeKind
{
    Solution,
    Project,
    Document,
    Namespace,
    Type,
    Member,
    UsingDirective,
    LocalVariable
}

public enum EdgeKind
{
    Contains,
    ProjectReference,
    UsingDirective,
    AliasTarget,
    GlobalUsingAppliesToProject,
    Inherits,
    Implements,
    TypeReference,
    MemberReference,
    MethodInvocation,
    AttributeUsage,
    DeclaredAt,
    ReferencedAt,
    DefinedAt,
    UsedAt,
    UseBeforeAssign
}

public sealed record LocationSpan(
    string FilePathRelative,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

public sealed record GraphNode(
    long Id,
    NodeKind Kind,
    string Name,
    string? FullyQualifiedName,
    string? DocumentPath,
    LocationSpan? Location);

public sealed record GraphEdge(
    long Id,
    EdgeKind Kind,
    long SourceId,
    long TargetId,
    LocationSpan? Location);

public sealed record QueryResult(
    string SchemaVersion,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record QueryRequest(
    string QueryType,
    string? Symbol,
    string? Kind,
    string? Document,
    string? EdgeKind,
    int Limit,
    int ContextLines);

public static class SchemaConstants
{
    public const string CurrentVersion = "2";
}

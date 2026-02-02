# RagSharp

RagSharp is a deterministic, offline-capable toolkit for indexing and querying C#/.NET repositories.
It ships as a single NuGet package that provides two command-line tools:

- `ragsharp` — installs/uninstalls Codex skills into a repository.
- `ragsharp graph ...` — builds and queries a code graph index for fast, structured lookups.
- `ragsharp-mcp` — JSON-RPC MCP server exposing doctor/index/update/query/export over the code graph.

See the Codex skill specification at https://agentskills.io/specification.

## What RagSharp does

RagSharp focuses on repeatable, offline analysis of C# codebases:

- **Indexes** source code and builds a graph of types, members, usings, and relationships.
- **Queries** the graph (symbols, inheritance, references) and outputs JSON for automation.
- **Installs skills** that wrap common workflows such as indexing or querying.

All outputs are stored under `.ragsharp/graph/` by default to keep artifacts organized and
easy to clean up.

## Requirements

- .NET SDK 10 (only target).
- Cross-platform (Windows/macOS/Linux).

## Install .NET 10

Follow the OS-specific steps in [docs/Development/SetupDotNet.md](docs/Development/SetupDotNet.md) and verify:

```bash
dotnet --info
dotnet --list-sdks
```

## Build and test RagSharp

```bash
dotnet build

dotnet test ragsharp.slnx
```

## Package

```bash
dotnet pack src/RagSharp.Packaging/RagSharp.Packaging.csproj -c Release -o dist
```

## Install in another repository

From the target repository root:

```bash
dotnet add package RagSharp --source /path/to/ragsharp/dist

ragsharp install --root . --skill-dir .codex/skills
```

## RagSharp Graph quickstart

Build and query the graph:

```bash
ragsharp graph doctor --root .

ragsharp graph index --root . --db .ragsharp/graph/index.db --state .ragsharp/graph/state.json

ragsharp graph query --type symbols --db .ragsharp/graph/index.db --limit 50 --symbol "Greeter"
```

Export the graph:

```bash
ragsharp graph export --db .ragsharp/graph/index.db --format dot --out .ragsharp/graph/graph.dot
```

## MCP server quickstart

Start the server (reads stdin/writes stdout) and send JSON-RPC:

```
ragsharp-mcp
```

Example request (doctor):

```json
{"jsonrpc":"2.0","id":1,"method":"doctor","params":{"root":"."}}
```

Supported methods: `doctor`, `index`, `update`, `query`, `export` (parameters match CLI options: root/db/state/includeDataflow/symbol/kind/document/edgeKind/limit/contextLines/format/output).

## CLI reference

### `ragsharp`

- `ragsharp install --root <path> --skill-dir <path>` — install skills.
- `ragsharp uninstall --root <path> --skill-dir <path>` — uninstall skills.

### `ragsharp graph`

- `doctor` — verify the environment and print the repository root.
- `index` — build a fresh index.
- `update` — incrementally update the index and remove deleted files.
- `query --type <type>` — query the index (`symbols`, `usings`, etc.) and emit JSON.
- `export` — export the graph as `dot` or `gexf`.

## Output locations

- `.codex/skills/` contains installed skills.
- `.ragsharp/graph/` contains the index and state files (not committed).
- Ensure `.ragsharp/graph/` and `state.json` remain in `.gitignore`.

## Example prompts

Use these with your assistant after indexing:

- “List the top 20 symbols that inherit from `BaseType` in JSON.”
- “Show usages of `HttpClient` and include 3 context lines.”
- “Find all public methods in `src/` that return `Task`.”
- “Export the graph to DOT and summarize the top 10 node kinds.”

## Troubleshooting

See [docs/Development/Troubleshooting.md](docs/Development/Troubleshooting.md).

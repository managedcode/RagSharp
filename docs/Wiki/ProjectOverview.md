# RagSharp Project Overview

RagSharp provides code graph indexing, storage, and tooling for analyzing .NET codebases.
The core library builds a graph of symbols and relationships (types, members, inheritance,
and references). A lightweight SQLite-backed store allows querying that graph, while the
CLI and packaging utilities wrap the core capabilities for local workflows and publishing.

## Key Components

- **Code graph indexing**: `src/RagSharp.CodeGraph.Core` uses Roslyn + MSBuild workspace
  to parse solutions/projects and emit nodes/edges.
- **Graph storage**: `src/RagSharp.CodeGraph.Store.LiteGraph` stores the graph in SQLite
  and supports symbol/document queries.
- **CLI**: `src/RagSharp.CodeGraph.Cli` provides command-line access for indexing.
- **Skill installer**: `src/RagSharp.SkillInstaller` installs bundled skill templates.
- **Packaging**: `src/RagSharp.Packaging` handles packaging and publishing needs.

## Build & Test

```bash
dotnet build ragsharp.slnx
dotnet test ragsharp.slnx
```

## Repository Conventions

- Target .NET SDK **10.x** only (`global.json`).
- Use the `.slnx` solution format.
- Tests are written with **TUnit** and favor integration coverage.

# RagSharp

RagSharp provides a deterministic, offline-capable code graph indexer and skill installer for C#/.NET repositories. It ships a single NuGet package containing:

- `ragsharp` — installer for Codex skills.
- `ragsharp-codegraph` — code graph indexer/query CLI.

See the Codex skill specification at https://agentskills.io/specification.

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

dotnet test
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

## Index and query

```bash
ragsharp-codegraph doctor --root .

ragsharp-codegraph index --root . --db .codegraph/index.db --state .codegraph/state.json

ragsharp-codegraph query symbols --db .codegraph/index.db --format json --limit 50 --symbol "Greeter"
```

## Output locations

- `.codex/skills/` contains installed skills.
- `.codegraph/` contains the index and state files (not committed).
- Ensure `.codegraph/` and `state.json` remain in `.gitignore`.

## Troubleshooting

See [docs/Development/Troubleshooting.md](docs/Development/Troubleshooting.md).

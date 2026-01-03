# Repository Agent Instructions (MCAF)

This repository follows the Managed Code Coding AI Framework (MCAF).

## Environment
- Use .NET SDK **10.x** only (see `global.json`).
- Solution format is **.slnx** (`ragsharp.slnx`). Do not reintroduce `.sln` files.
- Tests use **TUnit**. Avoid xUnit/NUnit/MSTest packages.

## Verification
- Prefer integration tests that exercise real filesystem/indexing behavior.
- Run `dotnet test ragsharp.slnx` after changes when feasible.

## Repository Layout
- Source projects live under `src/`.
- Tests live under `tests/`.

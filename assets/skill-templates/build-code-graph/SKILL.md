---
name: ragsharp-build-code-graph
description: |
  Build or update a code graph index for C#/.NET repositories using ragsharp-codegraph.
  Triggers: build index, update index, refresh index, code graph, dependency graph, static analysis, Roslyn, line numbers.
---
## Steps
1. Run `ragsharp-codegraph doctor --root .`.
2. Run `ragsharp-codegraph index --root . --db .codegraph/index.db --state .codegraph/state.json`.
3. For incremental updates, run `ragsharp-codegraph update --root . --db .codegraph/index.db --state .codegraph/state.json`.

## Expected Results
- `.codegraph/index.db` exists.
- `.codegraph/state.json` is updated.
- Output goes to stderr, JSON is emitted only for query commands.

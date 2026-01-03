# RagSharp Graph CLI

## Commands

- `ragsharp-graph doctor --root <path>`
- `ragsharp-graph index --root <path> --db .ragsharp/graph/index.db --state .ragsharp/graph/state.json`
- `ragsharp-graph update --root <path> --db .ragsharp/graph/index.db --state .ragsharp/graph/state.json`
- `ragsharp-graph query <queryType> --db .ragsharp/graph/index.db --format json --limit N --context-lines M`
- `ragsharp-graph export --db .ragsharp/graph/index.db --format dot|gexf --out .ragsharp/graph/graph.dot`

## Files

- `.ragsharp/graph/index.db` — SQLite graph database.
- `.ragsharp/graph/state.json` — incremental index state.
- `.ragsharp/graph/schema_version` — schema version guard.
## Exit codes

- 0: success
- 2: invalid arguments
- 3: environment error
- 4: index missing
- 5: schema mismatch

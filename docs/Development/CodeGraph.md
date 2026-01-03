# Code Graph CLI

## Commands

- `ragsharp-codegraph doctor --root <path>`
- `ragsharp-codegraph index --root <path> --db .codegraph/index.db --state .codegraph/state.json`
- `ragsharp-codegraph update --root <path> --db .codegraph/index.db --state .codegraph/state.json`
- `ragsharp-codegraph query <queryType> --db .codegraph/index.db --format json --limit N --context-lines M`
- `ragsharp-codegraph export --db .codegraph/index.db --format dot|gexf --out .codegraph/graph.dot`

## Files\n\n- `.codegraph/index.db` — SQLite graph database.\n- `.codegraph/state.json` — incremental index state.\n- `.codegraph/schema_version` — schema version guard.\n+
## Exit codes

- 0: success
- 2: invalid arguments
- 3: environment error
- 4: index missing
- 5: schema mismatch

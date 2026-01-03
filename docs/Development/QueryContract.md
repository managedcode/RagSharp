# Query Contract

All `ragsharp-graph query` commands emit JSON to stdout. Logs go to stderr. The JSON schema is defined in [OutputSchema.md](OutputSchema.md).

Query requests support:
- `--symbol` to filter by name or fully-qualified name.
- `--kind` to filter by node kind.
- `--document` to filter by file path.
- `--limit` to cap results.

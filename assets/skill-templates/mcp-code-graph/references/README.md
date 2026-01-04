Use these docs to wire `ragsharp-mcp` into your MCP-compatible agent.

## Protocol
- JSON-RPC 2.0 over stdio. One method per request; responses include `id`.
- Methods: `doctor`, `index`, `update`, `query`, `export`.
- Params:
  - `root`: source tree to index.
  - `db`: LiteGraph DB path (e.g. `.ragsharp/graph/index.db`).
  - `state`: index state path (optional; keeps incremental state).
  - `type`: query type (`symbols` for now).
  - `symbol`: symbol name to filter (case-sensitive).
  - `kind`: node kind filter.
  - `document`: document path filter (relative to root).
  - `edgeKind`: edge kind filter (e.g. `Reference`).
  - `limit`: max results.
  - `contextLines`: extra lines around source location.
  - `format`: `dot` or `gexf`.
  - `output`: export file path.

## Examples
- Doctor: `{"jsonrpc":"2.0","id":1,"method":"doctor","params":{"root":"."}}`
- Index: `{"jsonrpc":"2.0","id":2,"method":"index","params":{"root":".","db":".ragsharp/graph/index.db","state":".ragsharp/graph/state.json"}}`
- Update: `{"jsonrpc":"2.0","id":3,"method":"update","params":{"root":".","db":".ragsharp/graph/index.db","state":".ragsharp/graph/state.json"}}`
- Query: `{"jsonrpc":"2.0","id":4,"method":"query","params":{"db":".ragsharp/graph/index.db","type":"symbols","symbol":"Greeter","edgeKind":null,"limit":20,"contextLines":2}}`
- Export: `{"jsonrpc":"2.0","id":5,"method":"export","params":{"db":".ragsharp/graph/index.db","format":"gexf","output":".ragsharp/graph/graph.gexf"}}`

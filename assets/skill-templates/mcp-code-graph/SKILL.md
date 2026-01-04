---
name: ragsharp-mcp-code-graph
description: |
  Run the ragsharp MCP JSON-RPC server and issue graph commands (doctor/index/update/query/export) for Codex/agent integrations.
  Triggers: mcp, json-rpc, code graph, references, index, query, export.
---
## Steps
1. Start the MCP server: `ragsharp-mcp` (reads stdin, writes stdout).
2. Send JSON-RPC 2.0 requests, e.g. `{"jsonrpc":"2.0","id":1,"method":"doctor","params":{"root":"."}}`.
3. For indexing: `{"jsonrpc":"2.0","id":2,"method":"index","params":{"root":".","db": ".ragsharp/graph/index.db","state": ".ragsharp/graph/state.json"}}`.
4. For querying: `{"jsonrpc":"2.0","id":3,"method":"query","params":{"db": ".ragsharp/graph/index.db","type":"symbols","symbol":"Greeter","edgeKind":null,"limit":50}}`.
5. For export: `{"jsonrpc":"2.0","id":4,"method":"export","params":{"db": ".ragsharp/graph/index.db","format":"dot","output": ".ragsharp/graph/graph.dot"}}`.

## Expected Results
- Server responds with JSON-RPC 2.0 responses containing `result` or `error`.
- Indexing returns counts of nodes/edges and writes `.ragsharp/graph/index.db` and `state.json`.
- Query returns `nodes` and `edges` matching filters (`type`, `symbol`, `kind`, `document`, `edgeKind`, `limit`, `contextLines`).
- Export writes DOT or GEXF to the requested path.

#!/usr/bin/env bash
set -euo pipefail

# Starts ragsharp-mcp and sends a sample query over JSON-RPC 2.0.
# Requires: dotnet tool install --global RagSharp.Mcp (or local build in repo)

ROOT=${1:-.}
DB=${2:-.ragsharp/graph/index.db}
STATE=${3:-.ragsharp/graph/state.json}

# Start server in background reading stdin; we pipe a request then exit.
cat <<'EOF' | ragsharp-mcp --root "$ROOT" --db "$DB" --state "$STATE"
{"jsonrpc":"2.0","id":1,"method":"doctor","params":{"root":"."}}
{"jsonrpc":"2.0","id":2,"method":"index","params":{"root":".","db":".ragsharp/graph/index.db","state":".ragsharp/graph/state.json"}}
{"jsonrpc":"2.0","id":3,"method":"query","params":{"db":".ragsharp/graph/index.db","type":"symbols","symbol":null,"limit":5}}
EOF

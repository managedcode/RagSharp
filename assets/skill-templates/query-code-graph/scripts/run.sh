#!/usr/bin/env bash
set -euo pipefail
ragsharp-graph query symbols --db .ragsharp/graph/index.db --format json --limit 50 --symbol "${1:-}" 

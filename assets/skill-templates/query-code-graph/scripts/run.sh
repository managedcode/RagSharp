#!/usr/bin/env bash
set -euo pipefail
ragsharp graph query --type symbols --db .ragsharp/graph/index.db --limit 50 --symbol "${1:-}" 

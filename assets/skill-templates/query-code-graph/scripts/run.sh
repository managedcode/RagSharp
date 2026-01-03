#!/usr/bin/env bash
set -euo pipefail
ragsharp-codegraph query symbols --db .codegraph/index.db --format json --limit 50 --symbol "${1:-}" 

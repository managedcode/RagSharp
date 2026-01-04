#!/usr/bin/env bash
set -euo pipefail
ragsharp graph index --root "${1:-.}" --db .ragsharp/graph/index.db --state .ragsharp/graph/state.json

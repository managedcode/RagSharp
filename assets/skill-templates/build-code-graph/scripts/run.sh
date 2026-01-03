#!/usr/bin/env bash
set -euo pipefail
ragsharp-codegraph index --root "${1:-.}" --db .codegraph/index.db --state .codegraph/state.json

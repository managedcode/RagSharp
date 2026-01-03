param([string]$Root = ".")
ragsharp-codegraph index --root $Root --db .codegraph/index.db --state .codegraph/state.json

param([string]$Symbol = "")
ragsharp-codegraph query symbols --db .codegraph/index.db --format json --limit 50 --symbol $Symbol

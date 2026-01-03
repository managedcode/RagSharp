param([string]$Symbol = "")
ragsharp-graph query symbols --db .ragsharp/graph/index.db --format json --limit 50 --symbol $Symbol

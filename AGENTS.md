# AGENTS.md

Project: RagSharp — deterministic C#/.NET code graph + skills toolkit + mcp
Stack: C# 12 on .NET SDK 10.0 (see `global.json`); Roslyn (workspace + CSharp), LiteGraphDB (https://github.com/litegraphdb/litegraph) for storage, `System.CommandLine`, TUnit tests. Solution format: `.slnx` only (`RagSharp.slnx`). Central package versions in `Directory.Packages.props`. Follows [MCAF](https://mcaf.managed-code.com/).

---

## Conversations (Self-Learning)

Learn the user's habits, preferences, and working style. Extract rules from conversations, save to "## Rules to follow", and generate code according to the user's personal rules.

**Update requirement (core mechanism):**

Before doing ANY task, evaluate the latest user message.  
If you detect a new rule, correction, preference, or change → update `AGENTS.md` first.  
Only after updating the file you may produce the task output.  
If no new rule is detected → do not update the file.

**When to extract rules:**

- prohibition words (never, don't, stop, avoid) or similar → add NEVER rule
- requirement words (always, must, make sure, should) or similar → add ALWAYS rule
- memory words (remember, keep in mind, note that) or similar → add rule
- process words (the process is, the workflow is, we do it like) or similar → add to workflow
- future words (from now on, going forward) or similar → add permanent rule

**Preferences → add to Preferences section:**

- positive (I like, I prefer, this is better) or similar → Likes
- negative (I don't like, I hate, this is bad) or similar → Dislikes
- comparison (prefer X over Y, use X instead of Y) or similar → preference rule

**Corrections → update or add rule:**

- error indication (this is wrong, incorrect, broken) or similar → fix and add rule
- repetition frustration (don't do this again, you ignored, you missed) or similar → emphatic rule
- manual fixes by user → extract what changed and why

**Strong signal (add IMMEDIATELY):**

- swearing, frustration, anger, sarcasm → critical rule
- ALL CAPS, excessive punctuation (!!!, ???) → high priority
- same mistake twice → permanent emphatic rule
- user undoes your changes → understand why, prevent

**Ignore (do NOT add):**

- temporary scope (only for now, just this time, for this task) or similar
- one-off exceptions
- context-specific instructions for current task only

**Rule format:**

- One instruction per bullet
- Tie to category (Testing, Code, Docs, etc.)
- Capture WHY, not just what
- Remove obsolete rules when superseded

---

## Rules to follow (Mandatory, no exceptions)

### Commands

- build: `dotnet build ragsharp.slnx`
- test: `dotnet test ragsharp.slnx`
- pack: `dotnet pack src/RagSharp.Packaging/RagSharp.Packaging.csproj -c Release -o dist`
- format: _not configured in repo (no formatter/command present)_
- analyze/coverage: _none configured_

### Task Delivery (ALL TASKS)

- Architecture-first: start at `docs/Architecture/Overview.md` (present; keep diagrams and links current). Use `docs/Development/*` and `docs/Wiki/ProjectOverview.md` for detail.
- Identify impacted modules: CLI (`RagSharp.SkillInstaller`), code graph core (`RagSharp.CodeGraph.Core`), store (`RagSharp.CodeGraph.Store.LiteGraph`), packaging, and tests under `tests/`.
- Respect .slnx-only workflow (`RagSharp.slnx`); do not introduce `.sln`.
- Follow existing git patterns: main branch is default; merges often come via PRs (`Merge pull request #N ...`); local commits are short, imperative titles.
- Use skills templates under `assets/skill-templates/` when tasks match (build/query code graph); keep them updated if behaviour changes.
- Plan before implementing; update docs/skills when behaviour changes. Implement code and tests together. Run build before test when separate. Summarize changes and executed commands.

### Documentation (ALL TASKS)

- All docs live in `docs/` (or `.wiki/`)
- Global architecture entry point: `docs/Architecture/Overview.md` (read first)
- Single source of truth (no duplication):
  - each important fact/rule/diagram lives in exactly one canonical place; other docs should link, not copy
  - `docs/Architecture/Overview.md` is coarse and navigational (diagrams + links), not the place for detailed behaviour
  - detailed behaviour belongs in `docs/Features/*`; detailed decisions/invariants belong in `docs/ADR/*`
  - if you need detail, follow the link; only add new text when there is no canonical place yet
- When creating docs from templates:
  - copy the template into its real docs location
  - replace all placeholders and remove all template notes (real docs must be clean: no `TEMPLATE ONLY`, `TODO:`, `...`)
- Update feature docs when behaviour changes
- Update ADRs when architecture changes
- Diagrams are mandatory in docs:
  - `docs/Architecture/Overview.md`: Mermaid diagrams for system/modules + interfaces/contracts + key classes/types (must render)
    - every diagram element has a real reference link in the same file (docs/code), so an agent can navigate without repo-wide scanning
  - `docs/Features/*`: at least one Mermaid diagram for the main flow
  - `docs/ADR/*`: at least one Mermaid diagram for the decision
- Mermaid hygiene (Mermaid often breaks if you freestyle it):
  - diagrams must render in repo Markdown preview (broken Mermaid is treated as broken documentation)
  - prefer simple Mermaid syntax (`flowchart` / `sequenceDiagram`); use short ASCII-only IDs
  - if `classDiagram` is flaky in your renderer, replace it with a `flowchart` that shows the same relationships
  - if a diagram doesn’t render, simplify it until it does (no “close enough”)

### Testing (ALL TASKS)

- Test framework: TUnit via Microsoft.Testing.Platform (`EnableTestingPlatform`); tests are exe-style projects under `tests/`.
- Suites: `RagSharp.CodeGraph.Tests` (indexes synthetic project and queries graph), `RagSharp.SkillInstaller.Tests` (install/uninstall manifest flow).
- Command: `dotnet test ragsharp.slnx` (covers all test projects).
- Tests favor integration-style flows: real temporary projects, SQLite-backed store, no mocks. Keep that pattern; avoid mocking internal graph/store.
- Add new tests alongside features; ensure meaningful assertions (graph nodes/edges, manifest files). Keep data-driven cases for breadth.

### Autonomy

- Start work immediately — no permission seeking
- Questions only for architecture blockers not covered by ADR
- Report only when task is complete

### Advisor stance (ALL TASKS)

- Stop being agreeable: be direct and honest; no flattery, no validation, no sugar-coating.
- Challenge weak reasoning; point out missing assumptions and trade-offs.
- If something is underspecified/contradictory/risky — say so and list what must be clarified.
- Never guess or invent. If unsure, say “I don’t know” and propose how to verify.
- Quality and security first: tests + static analysis are gates; treat security regressions as blockers.

### Code Style

- C# with implicit usings and nullable enabled; PascalCase types/methods; switch expressions minimal; expression-bodied members used sparingly.
- Logging mostly absent; keep console stderr for CLI status.
- Project props: `net10.0` target; do not downgrade.
- Solution uses `.slnx`; keep file/folder naming in PascalCase for projects and snake-free.
- Avoid magic literals; extract to constants where reused.

### Critical (NEVER violate)

- Never commit secrets, keys, connection strings
- Never mock internal systems in integration tests
- Never skip tests to make PR green
- Never force push to main
- Never approve or merge (human decision)

### Boundaries

**Always:**

- Respect `.slnx` solution format and .NET 10 SDK (`global.json`).
- Preserve central package management (`Directory.Packages.props`).
- Keep skill templates under `assets/skill-templates/` in sync with behaviour; do not remove manifests.
- Keep `.ragsharp/graph/` outputs out of source (see `.gitignore`).
- Read AGENTS.md and docs before editing; run tests before committing.

**Ask first:**

- Changing public CLI surface (`ragsharp` commands) or graph schema versions.
- Adding new dependencies or altering package IDs.
- Modifying storage schema (SQLite tables) or migration paths.
- Deleting code files or moving project boundaries.

---

## Preferences

### Likes

### Dislikes
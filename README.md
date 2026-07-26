# GraphTools

Offline C# code knowledge graph via Roslyn — build, query, diff, and visually explore
call/dependency graphs for any .NET solution, small or large.

## Why this exists

Tools like GitHub Copilot work better when they have an accurate structural map of a codebase
— which types call which, what implements what, how projects depend on each other — instead of
re-reading and re-guessing that structure from scratch in every session. GraphTools builds that
map once, locally, and lets Copilot (or you) query it cheaply and repeatedly.

- **No source code ever leaves your machine.** Extraction runs entirely through Roslyn's
  semantic model, locally. There is no hosted API call, no LLM summarization step, nothing sent
  anywhere. This matters if you work on proprietary or confidential code.
- **Token-efficient for AI agents.** Instead of an agent re-scanning dozens of files to answer
  "who calls this method," it runs one fast local query and gets an exact answer.
- **Grounded, not guessed.** A queryable graph gives an agent deterministic facts instead of a
  plausible-sounding but possibly wrong answer synthesized from partial context.
- **Safer refactoring suggestions.** An agent can check real callers/callees before suggesting
  a change, catching usages it might otherwise miss in a codebase it hasn't fully read.
- **Drift detection.** Diffing two graphs (e.g., start of session vs. end of session) surfaces
  structural changes deterministically — additions, removals, signature changes — rather than
  relying on an agent's own judgment to notice what changed.
- **Fast onboarding for a fresh session.** A new chat/agent session can query the graph to
  understand an unfamiliar large codebase quickly, similar to how a good architecture diagram
  helps a new human developer.
- **Works at any scale.** Useful on a small side project just as much as a large, multi-project
  solution — the smaller the project, the faster it runs; the larger the project, the more
  valuable a structural map becomes.

## What's included

| Component | Type | Purpose |
|---|---|---|
| `GraphTools.Core` | class library | Shared JSON models, Roslyn workspace-loading logic |
| `GraphTools.Builder` | console app | Builds/updates the graph (full, incremental, diff modes) |
| `GraphTools.Query` | console app | Looks up a specific symbol without loading the whole graph |
| `graph-viewer.html` | standalone HTML file | Interactive, offline visual explorer for the graph |

No UI application, no installer — everything is a console tool or a static file that runs
directly, so there's nothing to maintain beyond the source itself.

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022+ (or `dotnet build` from the CLI) to build the solution
- NuGet access to restore `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`,
  and `Microsoft.Build.Locator` — if your environment's default NuGet feed can't resolve these
  (e.g. an org-managed feed that doesn't mirror them), add a `NuGet.Config` scoped to this
  folder pointing at nuget.org.

## Building

```
dotnet build GraphTools.sln
```

Produces:
- `GraphTools.Builder\bin\Debug\net8.0\GraphTools.Builder.exe`
- `GraphTools.Query\bin\Debug\net8.0\GraphTools.Query.exe`

## Usage

### Build a graph

```
GraphTools.Builder.exe --solution "<path to .sln or .slnx>" --output "<path>\full-graph.json" --mode full
```

Also produces `project-dependencies.json` in the same output folder — a small, always-safe-to-
load-in-full file listing which projects reference which.

### Update a graph incrementally

```
GraphTools.Builder.exe --solution "<path>" --output "<path>\full-graph.json" --mode incremental --graph "<path>\full-graph.json"
```

Only re-analyzes files that changed since the existing graph's `generatedAt` timestamp
(detected via file `LastWriteTimeUtc`), rather than rebuilding the whole solution. `obj/`/`bin/`
paths are excluded from change detection, since design-time builds for WPF/XAML projects
regenerate `.g.cs` files on every workspace load regardless of real edits — without this
exclusion, incremental mode would falsely treat those as changed every run.

### Diff two graphs

```
GraphTools.Builder.exe --diff "<old-graph.json>" "<new-graph.json>" --output "<diff-report.json>"
```

Pure JSON-to-JSON comparison (no Roslyn/workspace loading) — reports added/removed/modified
nodes and edges, compared by stable symbol ID, not file position.

### Query a symbol

```
GraphTools.Query.exe --graph "<full-graph.json>" --symbol "<fully-qualified-id>"
GraphTools.Query.exe --graph "<path>" --symbol "<id>" --direction callers
GraphTools.Query.exe --graph "<path>" --symbol "<id>" --direction callees
GraphTools.Query.exe --graph "<path>" --list-symbols --project "<project name>"
```

`--list-symbols` is useful for discovering a symbol's exact ID when you don't already know it.
All output is JSON, meant to be read by an agent or piped into further tooling — never load
`full-graph.json` wholesale into an LLM's context; always query a specific symbol instead.

## Visual graph viewer

`graph-viewer.html` is a single, dependency-free HTML file (uses D3.js via CDN) — copy it next
to a generated `full-graph.json`/`project-dependencies.json` and open it in a browser. No
server, no build step.

Features:
- **Two-level exploration**: opens on a project-to-project dependency view, click a project to
  drill into its internal type-level dependency graph, click a type to see its members.
- **Search**, **zoom/pan**, **fullscreen**, **light/dark mode** (auto-detects system preference,
  remembers a manual override).
- **Directional arrows** on relationships, styled loosely after UML conventions: solid + bold
  chevron for `extends`, dashed + bold chevron for `implements`, thin chevron for
  `calls`/`uses`/`overrides`. Arrows can be toggled off.
- **Shape or icon mode** for node kind: plain geometric shapes (circle/diamond/square) or
  UML-style icons (class box, hollow interface circle, enum list, package folder-tab for
  projects) — user's choice via checkbox.
- **Abstract/sealed/static indicators**, shown as a badge (shape mode) or baked into the icon
  itself (dashed border / padlock / underline, in icon mode).
- **Member accessibility symbols** in the side panel, using standard UML notation (`+` public,
  `-` private, `#` protected, `~` internal).
- **Optional size scaling**: node size can reflect member count (type view) or file count
  (project view) instead of a fixed size.
- **Drag-and-drop / manual file picker fallback** — opening the file directly (`file://`) blocks
  `fetch()` of local JSON in most browsers; the viewer detects this and lets you drop or select
  the two JSON files instead.
- **Note**: Visual Studio's built-in "Internal Web Browser" cannot render this file — it uses a
  legacy engine with no modern JS support. Use "Browse With" → Edge/Chrome, or any standard
  browser, instead.

## Graph schema

`full-graph.json`:
```json
{
  "schemaVersion": "1.0",
  "generatedAt": "ISO timestamp",
  "solutionPath": "...",
  "mode": "full | incremental",
  "nodes": [ { "id", "kind", "name", "containingType", "project", "filePath",
               "startLine", "endLine", "accessibility", "isStatic", "isAbstract" } ],
  "edges": [ { "type", "sourceId", "targetId", "filePath", "line" } ]
}
```

- `kind`: `namespace | class | interface | struct | enum | method | property | field | event | constructor`
- `edge.type`: `calls | implements | extends | uses | overrides`
- **Node IDs are stable, fully-qualified identifiers** designed so the same symbol always gets
  the same ID across runs (required for diff mode to work):
  - Methods/indexers include parameter types, e.g. `Namespace.Type.Method(string,int)` —
    disambiguates overloads.
  - Generic methods get a `` `N `` arity suffix, e.g. `` Method`1(T) `` — disambiguates
    `Foo()` from `Foo<T>()`.
  - Conversion operators (`op_Implicit`/`op_Explicit`) additionally include the return type,
    the one case C# allows overloading by return type alone.
  - `ref`/`out`/`in` parameter modifiers are included in the parameter type string.
  - Partial class declarations are merged into a single node.

`project-dependencies.json` is a small, flat list of projects and their project references —
always safe to load in full, unlike `full-graph.json`.

## Known limitations

- Node line spans (`startLine`/`endLine`) reflect only the first partial-class declaration
  found, not a merged span across all partial files — a rough proxy for a type's size when
  partial classes are heavily used (e.g. WPF `.xaml.cs` code-behind), not a precise line count.
- `isSealed` is not currently extracted (only `isAbstract`/`isStatic` are) — the viewer already
  supports it if added later, but the extractor doesn't populate it yet.
- Tested primarily on Windows/.NET Framework + .NET 8 mixed solutions; not yet exercised across
  multiple developer machines or heavily non-WPF/non-legacy project shapes.

## Recommended `.gitignore` for consuming projects

`full-graph.json` and `project-dependencies.json` are fully regenerable (a full rebuild takes
well under a minute even on large solutions) and can be tens of MB — not worth committing to a
project's own repo:

```
full-graph.json
project-dependencies.json
```

## Using this with GitHub Copilot / Agent Skills

This tool pairs naturally with a Copilot Agent Skill that invokes `GraphTools.Builder.exe` at
the start of a session (full mode) and `--mode incremental` at the end of a session, writing
output alongside a project's other persistent memory files (e.g. a `docs/` folder) so Copilot
can query the graph on demand rather than re-exploring the codebase from scratch each time.

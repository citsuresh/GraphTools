# Project State

> This file is overwritten (not appended) at the end of each working session.

## Current Focus
- Completed project-memory-management-graph Initialize + Bootstrap; briefly upgraded to
  net10.0, then reverted to net8.0 after root-causing the launch failure as a `DOTNET_ROOT`
  environment override (from VS Insiders) rather than a real runtime gap; added
  `tools/Invoke-GraphTools.ps1` wrapper to work around it. Full graph rebuilt (97 nodes, 287 edges).

## Open Tasks / Bugs
- None identified. (Previously deferred: updating SKILL.md's GraphTools invocation guidance to
  use the wrapper script — done; skill bumped to v2, this project's marker updated to match.)

## Recently Changed Files
- `.github/prompts/begin-session.prompt.md`, `bootstrap.prompt.md`, `end-session.prompt.md`
- `.github/copilot-instructions.md` (Persistent Project Memory section regenerated; skill
  version marker added)
- `GraphTools.Core/GraphTools.Core.csproj`, `GraphTools.Builder/GraphTools.Builder.csproj`,
  `GraphTools.Query/GraphTools.Query.csproj` (net8.0 -> net10.0 -> reverted to net8.0)
- `tools/Invoke-GraphTools.ps1` (new: clears `DOTNET_ROOT` before invoking Builder/Query exes)
- `docs/full-graph.json`, `docs/project-dependencies.json` (rebuilt under net8.0 via wrapper)
- `docs/DESIGN_DECISIONS.md` (net10.0 upgrade decision, then reversal, both appended)

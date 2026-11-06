# Project State

> This file is overwritten (not appended) at the end of each working session.

## Current Focus
- 2026-11-06: Changed the default publish root to `C:\Shared` in the repository
  script and persistent publish instruction. OutputRoot overrides remain supported.
- Published and verified `C:\Shared\GraphTools-win-x64` (approximately 192 MB):
  self-contained Windows x64 Release Builder/Query, viewer, README, and usage notes.
- Builder smoke check: 99 nodes, 299 edges. Query: 81 Core symbols.
  Temporary verification graphs were removed.
- The prior bundle under Downloads\Shared was left untouched.

## Open Tasks / Bugs
- Destination-machine verification remains outstanding. Both tools initialize
  MSBuildLocator and need a compatible installed .NET SDK; Builder also needs the
  analyzed solution's targeting packs/workloads and restored dependencies.
- Existing build warning: Workspace.WorkspaceFailed is obsolete (CS0618).
- If the previously observed GitHub account-selection prompt recurs, investigate
  Windows/Git Credential Manager credentials; origin already embeds the username.

## Recently Changed Files
- `scripts\agent\Publish-GraphTools.ps1` (default root and help text).
- `.github\copilot-instructions.md` (publish destination).
- `docs/PROJECT_STATE.md` (this session snapshot).
- External bundle published to C:\Shared; no commits or pushes this session.

# Project State

> This file is overwritten (not appended) at the end of each working session.

## Current Focus
- 2026-11-05: Published Builder and Query as self-contained Windows x64 Release
  applications targeting net8.0 for use on another machine.
- Bundle: `C:\Users\sveluswa\Downloads\Shared\GraphTools-win-x64`
  (approximately 192 MB). Includes separate Builder/Query folders with dependencies,
  graph viewer, README, and START-HERE.txt with portable commands and prerequisites.
- Verified the published Builder against GraphTools.slnx (99 nodes, 299 edges) and
  the published Query (81 Core symbols). Temporary verification graphs were removed.
- Created and successfully ran `C:\Users\sveluswa\Downloads\Shared\Publish-GraphTools.ps1`.
  Rebuilds the timestamp-free bundle, copies docs, and smoke-checks both tools;
  accepts SourceRoot and OutputRoot overrides.
- Copied the script to `scripts\agent\Publish-GraphTools.ps1` and added a publish
  instruction in `.github\copilot-instructions.md` with an explicit Shared output path.

## Open Tasks / Bugs
- Destination-machine verification remains outstanding. Both tools initialize
  MSBuildLocator and need a compatible installed .NET SDK; Builder also needs the
  analyzed solution's targeting packs/workloads and restored dependencies.
- Existing build warning: Workspace.WorkspaceFailed is obsolete (CS0618).
- If the previously observed GitHub account-selection prompt recurs, investigate
  Windows/Git Credential Manager credentials; origin already embeds the username.

## Recently Changed Files
- `docs/PROJECT_STATE.md` (this session snapshot only; no source changes).
- External shared bundle created; no commits or pushes.

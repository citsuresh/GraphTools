# Design Decisions

This is an append-only, dated log of non-obvious architectural/design decisions. Never delete
or rewrite prior entries; if a decision is reversed, add a new entry referencing the old one.

## 2024 (inferred, date approximate) - Roslyn workspace via MSBuildWorkspace
- **Decision**: Load solutions using `Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace` with
  `MSBuildLocator.RegisterDefaults()` called once at process entry in each executable.
- **Rationale**: Enables accurate, compiler-grade symbol resolution across whole solutions
  without reimplementing a C# parser/binder.
- **Alternatives considered**: None recorded.

## (inferred) - Graph persisted as JSON with camelCase
- **Decision**: `FullGraph`/`ProjectDependencies`/`DiffReport` are serialized via
  `JsonHelper` using camelCase property naming and indented output.
- **Rationale**: Keeps output human-readable/diffable and consistent across tools that
  read/write the same schema (`SchemaVersion` field included for forward compatibility).

## (inferred) - Incremental mode merges by file path, not by node id
- **Decision**: Incremental builder detects changed files by last-write-time vs. the existing
  graph's `GeneratedAt`, then removes/replaces nodes and edges whose `FilePath` is in the
  changed set (rather than diffing per-symbol).
- **Rationale**: Simpler and safer than tracking per-symbol identity across edits; avoids
  stale entries for files that no longer exist in the affected project only if they were
  actually re-extracted.
- **Alternatives considered**: Full symbol-level incremental diff (rejected as unnecessary
  complexity for the current use case).

## (inferred) - Build-output files excluded from graph and change detection
- **Decision**: `PathUtils.IsInBuildOutputFolder` filters out any file path with a `bin` or
  `obj` path segment; `GraphExtractor` also skips types whose only declaration location is in
  such generated files (e.g., XAML `.g.cs`).
- **Rationale**: Avoids polluting the graph with generated/duplicate symbols and avoids
  spurious "changed file" detections from build artifacts.

## 2026-08-04 - Upgraded all projects from net8.0 to net10.0
- **Decision**: Changed `TargetFramework` from `net8.0` to `net10.0` in all three project
  files (`GraphTools.Core`, `GraphTools.Builder`, `GraphTools.Query`).
- **Rationale**: The local dev machine's Visual Studio Insiders install only ships the
  x64 `Microsoft.NETCore.App` 10.0.10 shared runtime (no x64 net8.0 runtime), so net8.0-built
  executables failed to launch (`You must install or update .NET to run this application`).
  net10.0 was already available locally, avoiding a separate runtime install.
- **Alternatives considered**: Installing the missing x64 .NET 8 runtime instead of retargeting
  (rejected — net10.0 was already present and simpler to standardize on).

## 2026-08-04 - Reverted to net8.0; added Invoke-GraphTools.ps1 wrapper instead
- **Decision**: Reversed the net10.0 upgrade above. Retargeted all three projects back to
  `net8.0`, and added `tools/Invoke-GraphTools.ps1`, a wrapper script that clears the
  `DOTNET_ROOT` environment variable before invoking `GraphTools.Builder.exe`/
  `GraphTools.Query.exe`, then delegates to the real exe.
- **Rationale**: Root-caused the original launch failure: it was never a real net8.0
  incompatibility — the x64 net8.0 shared runtime IS installed machine-wide
  (`C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.29`). The failure only happened
  because the calling PowerShell session had inherited `DOTNET_ROOT` from Visual Studio
  Insiders, pinned to its own bundled `net10.0` runtime folder, which the apphost respects
  over the machine-wide install. net10.0's SDK/runtime on this machine is also preview/RC
  (`NETSDK1057` warning), which is a weaker foundation than net8.0 LTS for a tool meant to be
  reused across many other projects/machines via this skill. Clearing `DOTNET_ROOT` in a
  wrapper script fixes the actual root cause without depending on a preview runtime or
  requiring every other machine/project using this tool to have net10 installed.
- **Alternatives considered**: Keeping net10.0 (rejected — depends on a preview SDK/runtime
  and would require net10 on every machine this tool is invoked from); permanently changing
  the machine/user-wide `DOTNET_ROOT` environment variable (rejected — Visual Studio Insiders
  sets it deliberately for its own embedded tooling; changing it globally risked breaking that
  unrelated behavior. A per-invocation wrapper is more targeted).

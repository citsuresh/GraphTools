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

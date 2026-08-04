using GraphTools.Core;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using System.Diagnostics;

MSBuildLocator.RegisterDefaults();

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        var options = ParseArgs(args);

        if (options.DiffMode)
        {
            RunDiff(options);
            return 0;
        }

        if (string.IsNullOrEmpty(options.Solution) || string.IsNullOrEmpty(options.Output))
        {
            Console.Error.WriteLine("Error: --solution and --output are required (unless using --diff).");
            return 1;
        }

        if (options.Mode == "incremental")
        {
            await RunIncrementalAsync(options);
        }
        else
        {
            await RunFullAsync(options);
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static async Task RunFullAsync(BuilderOptions options)
{
    Console.WriteLine("Loading workspace...");
    var solution = await WorkspaceLoader.LoadSolutionAsync(options.Solution!, msg => Console.WriteLine(msg));

    var commitSha = TryGetCommitSha(options.Solution);

    var graph = new FullGraph
    {
        GeneratedAt = DateTime.UtcNow,
        SolutionPath = options.Solution!,
        Mode = "full",
        CommitSha = commitSha,
    };

    var projects = solution.Projects.ToList();
    for (int i = 0; i < projects.Count; i++)
    {
        var project = projects[i];
        Console.WriteLine($"Analyzing project {i + 1} of {projects.Count}: {project.Name}...");
        var (nodes, edges) = await GraphExtractor.ExtractProjectAsync(project, msg => Console.WriteLine(msg));
        graph.Nodes.AddRange(nodes);
        graph.Edges.AddRange(edges);
    }

    Console.WriteLine("Writing output...");
    JsonHelper.WriteToFile(options.Output!, graph);

    var depsPath = GetProjectDepsPath(options.Output!);
    var deps = ProjectDependencyExtractor.Extract(solution);
    deps.CommitSha = commitSha;
    JsonHelper.WriteToFile(depsPath, deps);

    Console.WriteLine($"Wrote {graph.Nodes.Count} nodes and {graph.Edges.Count} edges to {options.Output}");
    Console.WriteLine($"Wrote project dependencies to {depsPath}");
}

static async Task RunIncrementalAsync(BuilderOptions options)
{
    if (string.IsNullOrEmpty(options.Graph))
    {
        throw new InvalidOperationException("--graph is required for incremental mode.");
    }

    Console.WriteLine("Loading existing graph...");
    var existingGraph = JsonHelper.ReadFromFile<FullGraph>(options.Graph);

    Console.WriteLine("Loading workspace...");
    var solution = await WorkspaceLoader.LoadSolutionAsync(options.Solution!, msg => Console.WriteLine(msg));

    var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var project in solution.Projects)
    {
        foreach (var doc in project.Documents)
        {
            if (doc.FilePath == null || !File.Exists(doc.FilePath) || PathUtils.IsInBuildOutputFolder(doc.FilePath))
            {
                continue;
            }

            if (File.GetLastWriteTimeUtc(doc.FilePath) > existingGraph.GeneratedAt)
            {
                changedFiles.Add(doc.FilePath);
            }
        }
    }

    Console.WriteLine($"Detected {changedFiles.Count} changed file(s).");

    var affectedProjects = solution.Projects
        .Where(p => p.Documents.Any(d => d.FilePath != null && changedFiles.Contains(d.FilePath)))
        .ToList();

    var newNodes = new List<GraphNode>();
    var newEdges = new List<GraphEdge>();
    for (int i = 0; i < affectedProjects.Count; i++)
    {
        var project = affectedProjects[i];
        Console.WriteLine($"Analyzing project {i + 1} of {affectedProjects.Count}: {project.Name}...");
        var (nodes, edges) = await GraphExtractor.ExtractProjectAsync(project, msg => Console.WriteLine(msg));

        // The extractor re-analyzes the whole project, but we only want to merge in results
        // that originated from files that actually changed; results from unaffected files in
        // the same project are already preserved from the existing graph below.
        newNodes.AddRange(nodes.Where(n => !string.IsNullOrEmpty(n.FilePath) && changedFiles.Contains(n.FilePath)));
        newEdges.AddRange(edges.Where(e => !string.IsNullOrEmpty(e.FilePath) && changedFiles.Contains(e.FilePath)));
    }

    // Remove old nodes/edges whose filePath is in the changed set, then merge in the new ones.
    var mergedNodes = existingGraph.Nodes
        .Where(n => string.IsNullOrEmpty(n.FilePath) || !changedFiles.Contains(n.FilePath))
        .Concat(newNodes)
        .ToList();

    var mergedEdges = existingGraph.Edges
        .Where(e => string.IsNullOrEmpty(e.FilePath) || !changedFiles.Contains(e.FilePath))
        .Concat(newEdges)
        .ToList();

    var commitSha = TryGetCommitSha(options.Solution);

    var mergedGraph = new FullGraph
    {
        GeneratedAt = DateTime.UtcNow,
        SolutionPath = options.Solution!,
        Mode = "incremental",
        Nodes = mergedNodes,
        Edges = mergedEdges,
        CommitSha = commitSha,
    };

    Console.WriteLine("Writing output...");
    JsonHelper.WriteToFile(options.Output!, mergedGraph);

    var depsPath = GetProjectDepsPath(options.Output!);
    var deps = ProjectDependencyExtractor.Extract(solution);
    deps.CommitSha = commitSha;
    JsonHelper.WriteToFile(depsPath, deps);

    Console.WriteLine($"Wrote {mergedGraph.Nodes.Count} nodes and {mergedGraph.Edges.Count} edges to {options.Output}");
}

static void RunDiff(BuilderOptions options)
{
    Console.WriteLine("Loading graphs...");
    var oldGraph = JsonHelper.ReadFromFile<FullGraph>(options.DiffOld!);
    var newGraph = JsonHelper.ReadFromFile<FullGraph>(options.DiffNew!);

    var oldNodesById = oldGraph.Nodes.ToDictionary(n => n.Id);
    var newNodesById = newGraph.Nodes.ToDictionary(n => n.Id);

    var report = new DiffReport
    {
        GeneratedAt = DateTime.UtcNow,
        OldGraph = options.DiffOld!,
        NewGraph = options.DiffNew!,
    };

    foreach (var id in newNodesById.Keys.Except(oldNodesById.Keys))
    {
        report.AddedNodes.Add(id);
    }

    foreach (var id in oldNodesById.Keys.Except(newNodesById.Keys))
    {
        report.RemovedNodes.Add(id);
    }

    foreach (var id in oldNodesById.Keys.Intersect(newNodesById.Keys))
    {
        var oldNode = oldNodesById[id];
        var newNode = newNodesById[id];
        var change = DetectChange(oldNode, newNode);
        if (change != null)
        {
            report.ModifiedNodes.Add(new ModifiedNode { Id = id, Change = change });
        }
    }

    string EdgeKeyString(GraphEdge e) => $"{e.Type}|{e.SourceId}|{e.TargetId}";
    var oldEdgeKeys = oldGraph.Edges.Select(EdgeKeyString).ToHashSet();
    var newEdgeKeys = newGraph.Edges.Select(EdgeKeyString).ToHashSet();

    foreach (var edge in newGraph.Edges.Where(e => !oldEdgeKeys.Contains(EdgeKeyString(e))))
    {
        report.AddedEdges.Add(new EdgeKey { Type = edge.Type, SourceId = edge.SourceId, TargetId = edge.TargetId });
    }

    foreach (var edge in oldGraph.Edges.Where(e => !newEdgeKeys.Contains(EdgeKeyString(e))))
    {
        report.RemovedEdges.Add(new EdgeKey { Type = edge.Type, SourceId = edge.SourceId, TargetId = edge.TargetId });
    }

    Console.WriteLine("Writing output...");
    JsonHelper.WriteToFile(options.Output!, report);
    Console.WriteLine($"Diff report written to {options.Output}");
}

static string? DetectChange(GraphNode oldNode, GraphNode newNode)
{
    var changes = new List<string>();

    if (!string.Equals(oldNode.FilePath, newNode.FilePath, StringComparison.OrdinalIgnoreCase))
    {
        changes.Add("moved file");
    }

    if (oldNode.Accessibility != newNode.Accessibility)
    {
        changes.Add("accessibility changed");
    }

    if (oldNode.IsStatic != newNode.IsStatic || oldNode.IsAbstract != newNode.IsAbstract)
    {
        changes.Add("signature changed");
    }

    if ((oldNode.StartLine != newNode.StartLine || oldNode.EndLine != newNode.EndLine) && !changes.Contains("moved file"))
    {
        changes.Add("signature changed");
    }

    return changes.Count > 0 ? string.Join(", ", changes.Distinct()) : null;
}

static string GetProjectDepsPath(string outputGraphPath)
{
    var dir = Path.GetDirectoryName(outputGraphPath);
    return string.IsNullOrEmpty(dir) ? "project-dependencies.json" : Path.Combine(dir, "project-dependencies.json");
}

static string? TryGetCommitSha(string? solutionPath)
{
    try
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            return null;
        }

        var repoRoot = Path.GetDirectoryName(Path.GetFullPath(solutionPath));
        if (string.IsNullOrEmpty(repoRoot))
        {
            return null;
        }

        var psi = new ProcessStartInfo("git", "rev-parse HEAD")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
    }
    catch
    {
        return null;
    }
}

static BuilderOptions ParseArgs(string[] args)
{
    var options = new BuilderOptions();

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--solution":
                options.Solution = args[++i];
                break;
            case "--output":
                options.Output = args[++i];
                break;
            case "--mode":
                options.Mode = args[++i];
                break;
            case "--graph":
                options.Graph = args[++i];
                break;
            case "--diff":
                options.DiffMode = true;
                options.DiffOld = args[++i];
                options.DiffNew = args[++i];
                break;
        }
    }

    return options;
}

class BuilderOptions
{
    public string? Solution { get; set; }
    public string? Output { get; set; }
    public string Mode { get; set; } = "full";
    public string? Graph { get; set; }
    public bool DiffMode { get; set; }
    public string? DiffOld { get; set; }
    public string? DiffNew { get; set; }
}

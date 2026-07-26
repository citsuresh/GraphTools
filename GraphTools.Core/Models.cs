using System.Text.Json.Serialization;

namespace GraphTools.Core;

public class GraphNode
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ContainingType { get; set; }
    public string Project { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Accessibility { get; set; } = "";
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
}

public class GraphEdge
{
    public string Type { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string? FilePath { get; set; }
    public int Line { get; set; }
}

public class FullGraph
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime GeneratedAt { get; set; }
    public string SolutionPath { get; set; } = "";
    public string Mode { get; set; } = "full";
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphEdge> Edges { get; set; } = new();
}

public class ProjectDependency
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public List<string> References { get; set; } = new();
}

public class ProjectDependencies
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime GeneratedAt { get; set; }
    public string SolutionPath { get; set; } = "";
    public List<ProjectDependency> Projects { get; set; } = new();
}

public class ModifiedNode
{
    public string Id { get; set; } = "";
    public string Change { get; set; } = "";
}

public class EdgeKey
{
    public string Type { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
}

public class DiffReport
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime GeneratedAt { get; set; }
    public string OldGraph { get; set; } = "";
    public string NewGraph { get; set; } = "";
    public List<string> AddedNodes { get; set; } = new();
    public List<string> RemovedNodes { get; set; } = new();
    public List<ModifiedNode> ModifiedNodes { get; set; } = new();
    public List<EdgeKey> AddedEdges { get; set; } = new();
    public List<EdgeKey> RemovedEdges { get; set; } = new();
}

using System.Text.Json;
using GraphTools.Core;
using Microsoft.Build.Locator;

MSBuildLocator.RegisterDefaults();

return Run(args);

static int Run(string[] args)
{
    try
    {
        var options = ParseArgs(args);

        if (string.IsNullOrEmpty(options.GraphPath))
        {
            Console.Error.WriteLine("Error: --graph is required.");
            return 1;
        }

        var graph = JsonHelper.ReadFromFile<FullGraph>(options.GraphPath);

        if (options.ListSymbols)
        {
            return RunListSymbols(graph, options);
        }

        if (string.IsNullOrEmpty(options.Symbol))
        {
            Console.Error.WriteLine("Error: --symbol is required (unless using --list-symbols).");
            return 1;
        }

        var node = graph.Nodes.FirstOrDefault(n => n.Id == options.Symbol);
        if (node == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = "symbol not found", symbol = options.Symbol }, JsonHelper.Options));
            return 1;
        }

        switch (options.Direction)
        {
            case "callers":
                return RunCallers(graph, options.Symbol!);
            case "callees":
                return RunCallees(graph, options.Symbol!);
            default:
                return RunDefault(graph, node);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static int RunDefault(FullGraph graph, GraphNode node)
{
    var incoming = graph.Edges.Count(e => e.TargetId == node.Id);
    var outgoing = graph.Edges.Count(e => e.SourceId == node.Id);

    var result = new
    {
        node,
        incomingEdgeCount = incoming,
        outgoingEdgeCount = outgoing,
    };

    Console.WriteLine(JsonSerializer.Serialize(result, JsonHelper.Options));
    return 0;
}

static int RunCallers(FullGraph graph, string symbolId)
{
    var nodesById = graph.Nodes.ToDictionary(n => n.Id);
    var results = graph.Edges
        .Where(e => e.TargetId == symbolId)
        .Select(e => new
        {
            edge = e,
            source = nodesById.TryGetValue(e.SourceId, out var n)
                ? new { n.Id, n.Kind, n.FilePath, n.StartLine }
                : null,
        })
        .ToList();

    Console.WriteLine(JsonSerializer.Serialize(results, JsonHelper.Options));
    return 0;
}

static int RunCallees(FullGraph graph, string symbolId)
{
    var nodesById = graph.Nodes.ToDictionary(n => n.Id);
    var results = graph.Edges
        .Where(e => e.SourceId == symbolId)
        .Select(e => new
        {
            edge = e,
            target = nodesById.TryGetValue(e.TargetId, out var n)
                ? new { n.Id, n.Kind, n.FilePath, n.StartLine }
                : null,
        })
        .ToList();

    Console.WriteLine(JsonSerializer.Serialize(results, JsonHelper.Options));
    return 0;
}

static int RunListSymbols(FullGraph graph, QueryOptions options)
{
    var query = graph.Nodes.AsEnumerable();
    if (!string.IsNullOrEmpty(options.Project))
    {
        query = query.Where(n => n.Project == options.Project);
    }

    var results = query.Select(n => new { n.Id, n.Kind, n.Name }).ToList();
    Console.WriteLine(JsonSerializer.Serialize(results, JsonHelper.Options));
    return 0;
}

static QueryOptions ParseArgs(string[] args)
{
    var options = new QueryOptions();

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--graph":
                options.GraphPath = args[++i];
                break;
            case "--symbol":
                options.Symbol = args[++i];
                break;
            case "--direction":
                options.Direction = args[++i];
                break;
            case "--list-symbols":
                options.ListSymbols = true;
                break;
            case "--project":
                options.Project = args[++i];
                break;
        }
    }

    return options;
}

class QueryOptions
{
    public string? GraphPath { get; set; }
    public string? Symbol { get; set; }
    public string? Direction { get; set; }
    public bool ListSymbols { get; set; }
    public string? Project { get; set; }
}

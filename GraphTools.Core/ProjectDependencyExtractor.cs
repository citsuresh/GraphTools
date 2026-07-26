using Microsoft.CodeAnalysis;

namespace GraphTools.Core;

public static class ProjectDependencyExtractor
{
    public static ProjectDependencies Extract(Solution solution)
    {
        var result = new ProjectDependencies
        {
            GeneratedAt = DateTime.UtcNow,
            SolutionPath = solution.FilePath ?? "",
        };

        foreach (var project in solution.Projects)
        {
            var references = project.ProjectReferences
                .Select(pr => solution.GetProject(pr.ProjectId)?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .OrderBy(name => name)
                .ToList();

            result.Projects.Add(new ProjectDependency
            {
                Name = project.Name,
                Path = project.FilePath ?? "",
                References = references,
            });
        }

        result.Projects = result.Projects.OrderBy(p => p.Name).ToList();
        return result;
    }
}

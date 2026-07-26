using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace GraphTools.Core;

public static class WorkspaceLoader
{
    /// <summary>
    /// Loads a solution via MSBuildWorkspace. Caller must have already called
    /// Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults() before invoking this.
    /// </summary>
    public static async Task<Solution> LoadSolutionAsync(string solutionPath, Action<string>? progress = null)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
        {
            progress?.Invoke($"Workspace warning: {e.Diagnostic.Message}");
        };

        var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter(progress));
        return solution;
    }

    private sealed class ConsoleProgressReporter : IProgress<ProjectLoadProgress>
    {
        private readonly Action<string>? _progress;

        public ConsoleProgressReporter(Action<string>? progress)
        {
            _progress = progress;
        }

        public void Report(ProjectLoadProgress value)
        {
            _progress?.Invoke($"Loading {value.Operation}: {Path.GetFileName(value.FilePath)}");
        }
    }
}

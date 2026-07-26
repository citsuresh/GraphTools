namespace GraphTools.Core;

public static class PathUtils
{
    /// <summary>
    /// Returns true if the given file path has "obj" or "bin" as a full path segment
    /// (case-insensitive), e.g. matches "...\obj\Debug\Foo.g.cs" but not "MyObjectHelper.cs".
    /// </summary>
    public static bool IsInBuildOutputFolder(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s.Equals("obj", StringComparison.OrdinalIgnoreCase) || s.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }
}

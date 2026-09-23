namespace PiuScoresWatcher.Tests.TestHelpers;

/// <summary>
///     Finds the checkout for the source-scanning ratchets: up from the test binaries to the folder
///     holding the solution file.
/// </summary>
internal static class RepositoryFiles
{
    private const string SolutionFile = "PiuScoresWatcher.sln";

    public static string Root { get; } = FindRoot();

    /// <summary>Every C# file under a top-level folder of the checkout, build output excluded.</summary>
    public static IEnumerable<string> Sources(string folder)
    {
        return Files(folder, "*.cs");
    }

    /// <summary>Every file matching <paramref name="pattern" /> under a top-level folder of the checkout, build output excluded.</summary>
    public static IEnumerable<string> Files(string folder, string pattern)
    {
        return Directory.EnumerateFiles(Path.Combine(Root, folder), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    public static string Relative(string path)
    {
        return Path.GetRelativePath(Root, path).Replace('\\', '/');
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new InvalidOperationException($"No {SolutionFile} above {AppContext.BaseDirectory}.");
    }
}

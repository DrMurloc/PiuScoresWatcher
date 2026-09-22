using System.Text.RegularExpressions;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.ArchitectureTests;

/// <summary>
///     The wall clock is read in exactly one place, the App's <c>SystemClock</c>; everything else
///     takes <c>IClock</c>, so a test can say what time it is. A direct call anywhere else fails here.
/// </summary>
public sealed partial class ClockSeamTests
{
    [GeneratedRegex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b")]
    private static partial Regex DirectClockCall();

    [Fact]
    public void OnlyTheSystemClockReadsTheWallClock()
    {
        var offenders = RepositoryFiles.Sources("src")
            .Where(file => Path.GetFileName(file) != "SystemClock.cs")
            .Where(file => DirectClockCall().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .ToArray();

        Assert.Empty(offenders);
    }
}

using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.ArchitectureTests;

/// <summary>
///     Velopack installs the watcher into its local-app-data folder and replaces that folder whole on
///     every install and removes it on uninstall, so anything the watcher kept there — the token, the
///     settings, the screens kept for review — would vanish with a reinstall. The watcher's own data
///     lives in the roaming profile; nothing under <c>src/</c> may even name the local one.
/// </summary>
public sealed class DataFolderTests
{
    [Fact]
    public void NothingIsKeptInVelopacksInstallFolder()
    {
        var offenders = RepositoryFiles.Sources("src")
            .Where(file => File.ReadAllText(file).Contains("SpecialFolder.LocalApplicationData", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .ToArray();

        Assert.Empty(offenders);
    }
}

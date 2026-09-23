using Velopack;
using Velopack.Sources;

namespace PiuScoresWatcher.App.Updates;

/// <summary>Where the watcher's releases live, and whether this copy is one Velopack installed.</summary>
public static class Installation
{
    public const string Repository = "https://github.com/DrMurloc/PiuScoresWatcher";

    public static UpdateManager Manager() => new(new GithubSource(Repository, accessToken: null, prerelease: false));

    /// <summary>False for a dev build run from bin/: nothing registers itself or updates itself then.</summary>
    public static bool IsInstalled => Manager().IsInstalled;
}

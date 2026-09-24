namespace PiuScoresWatcher.App;

/// <summary>The web pages the windows open.</summary>
public static class Links
{
    public const string Privacy = "https://github.com/DrMurloc/PiuScoresWatcher/blob/main/docs/PRIVACY.md";

    /// <summary>What changed in a version: its GitHub release, tagged <c>v0.2.0</c>.</summary>
    public static string Release(string version) => $"https://github.com/DrMurloc/PiuScoresWatcher/releases/tag/v{version}";

    /// <summary>Where a player makes a token: the site's account page.</summary>
    public static Uri TokenPage(Uri site) => new(site, "Account");

    public static void Open(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
}

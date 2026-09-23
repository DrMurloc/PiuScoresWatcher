namespace PiuScoresWatcher.App;

/// <summary>The web pages the windows open.</summary>
public static class Links
{
    public const string Privacy = "https://github.com/DrMurloc/PiuScoresWatcher/blob/main/docs/PRIVACY.md";

    /// <summary>Where a player makes a token: the site's account page.</summary>
    public static Uri TokenPage(Uri site) => new(site, "Account");

    public static void Open(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
}

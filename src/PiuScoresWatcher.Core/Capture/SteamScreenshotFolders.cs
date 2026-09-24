namespace PiuScoresWatcher.Core.Capture;

/// <summary>
///     Where Steam puts RISE's F12 screenshots: <c>&lt;Steam&gt;\userdata\&lt;account&gt;\760\remote\2756930\screenshots</c>,
///     one folder per Steam account that has played. The App finds the Steam root and the account
///     folders; this puts the path together and keeps the <c>thumbnails</c> subfolder out.
/// </summary>
public static class SteamScreenshotFolders
{
    /// <summary>RISE on Steam.</summary>
    public const string AppId = "2756930";

    public static IReadOnlyList<string> Resolve(string? steamRoot, IEnumerable<string> accountFolders, string? overrideFolder)
    {
        if (!string.IsNullOrWhiteSpace(overrideFolder))
            return [overrideFolder.Trim()];
        if (string.IsNullOrWhiteSpace(steamRoot))
            return [];
        return accountFolders
            .Where(account => account.All(char.IsAsciiDigit))
            .Select(account => Path.Combine(steamRoot, "userdata", account, "760", "remote", AppId, "screenshots"))
            .ToList();
    }

    /// <summary>A screenshot Steam wrote, not a thumbnail of one.</summary>
    public static bool IsScreenshot(string path)
    {
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(Path.GetFileName(Path.GetDirectoryName(path) ?? ""), "thumbnails", StringComparison.OrdinalIgnoreCase);
    }
}

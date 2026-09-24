namespace PiuScoresWatcher.Core.Startup;

/// <summary>
///     Which PIU Scores a run talks to, and what follows from it (D44). Production keeps the watcher's own
///     data folder and lock — it is the one copy a player runs. Any other site, a developer's local run,
///     keeps its data in a folder of its own and takes a lock of its own, so it runs beside an installed
///     copy and never reads or replaces that copy's token.
/// </summary>
public sealed record SiteScope(Uri Site)
{
    private const string Name = "PiuScoresWatcher";

    public static SiteScope Production { get; } = new(LaunchOptions.ProductionBaseUrl);

    /// <summary>The production site, whatever scheme, port or path it was written with.</summary>
    public bool IsProduction => string.Equals(Site.Host, LaunchOptions.ProductionBaseUrl.Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>The site as a person reads it: <c>localhost:7144</c>.</summary>
    public string Label => Site.IsDefaultPort ? Site.Host : $"{Site.Host}:{Site.Port}";

    /// <summary>The name of the one-copy lock: one running watcher per site.</summary>
    public string InstanceName => IsProduction ? Name : $"{Name}.dev.{FileName}";

    /// <summary><c>localhost-7144</c>: the site as a folder and a lock name — no character either would refuse.</summary>
    private string FileName => new($"{Site.Host}-{Site.Port}".ToLowerInvariant()
        .Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' ? c : '_')
        .ToArray());

    /// <summary>The folder everything this run writes lives in, under the roaming application-data folder.</summary>
    public string DataFolder(string roamingAppData)
    {
        var watcher = Path.Combine(roamingAppData, Name);
        return IsProduction ? watcher : Path.Combine(watcher, "dev", FileName);
    }
}

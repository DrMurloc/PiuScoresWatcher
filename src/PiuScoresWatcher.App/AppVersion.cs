using System.Reflection;

namespace PiuScoresWatcher.App;

/// <summary>The version the release workflow stamped from the tag; a dev build reads 0.1.0.</summary>
public static class AppVersion
{
    public static string Informational { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>The version a player reads — 0.1.0 — without the build's commit suffix.</summary>
    public static string Short { get; } = Informational.Split('+')[0];
}

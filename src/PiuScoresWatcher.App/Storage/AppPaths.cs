// WPF projects leave System.IO out of the implicit usings (System.Windows.Shapes.Path would clash).
using System.IO;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     Everything the watcher writes lives under one folder in the player's profile — settings,
///     the token, logs, and the frames kept for review — so "what does it keep" has one answer.
///     It is <c>%APPDATA%\PiuScoresWatcher</c>, never <c>%LOCALAPPDATA%\PiuScoresWatcher</c>: that
///     one is Velopack's install folder, replaced whole on install and removed on uninstall, and
///     anything kept there would go with it (arch-test enforced, DataFolderTests). A run against any
///     site but production keeps its own folder under it, <c>dev\&lt;host&gt;-&lt;port&gt;</c> (D44).
/// </summary>
public static class AppPaths
{
    private static readonly string RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string Root { get; private set; } = SiteScope.Production.DataFolder(RoamingAppData);

    /// <summary>Rolling daily logs, seven kept.</summary>
    public static string Logs => Path.Combine(Root, "logs");

    /// <summary>Frames the watcher could not turn into a recorded play, each with a note saying why, for the review window.</summary>
    public static string Failed => Path.Combine(Root, "failed");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>The personal token, DPAPI-encrypted for the Windows account; never inside the settings file.</summary>
    public static string TokenFile => Path.Combine(Root, "token.bin");

    /// <summary>Called once, first thing in <c>Main</c>, before anything reads or writes: the site decides the folder.</summary>
    public static void Use(SiteScope scope)
    {
        Root = scope.DataFolder(RoamingAppData);
    }

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Failed);
    }
}

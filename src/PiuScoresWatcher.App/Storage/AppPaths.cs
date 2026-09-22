// WPF projects leave System.IO out of the implicit usings (System.Windows.Shapes.Path would clash).
using System.IO;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     Everything the watcher writes lives under one folder in the player's local profile —
///     settings, logs, and the screens it could not read — so "what does it keep" has one answer.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiuScoresWatcher");

    /// <summary>Rolling daily logs, seven kept.</summary>
    public static string Logs { get; } = Path.Combine(Root, "logs");

    /// <summary>Result screens the reader could not parse, for the "send to the developer" button.</summary>
    public static string Failed { get; } = Path.Combine(Root, "failed");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Failed);
    }
}

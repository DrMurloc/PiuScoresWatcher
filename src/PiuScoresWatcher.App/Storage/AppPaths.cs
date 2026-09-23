// WPF projects leave System.IO out of the implicit usings (System.Windows.Shapes.Path would clash).
using System.IO;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     Everything the watcher writes lives under one folder in the player's profile — settings,
///     the token, logs, and the screens it could not read — so "what does it keep" has one answer.
///     It is <c>%APPDATA%\PiuScoresWatcher</c>, never <c>%LOCALAPPDATA%\PiuScoresWatcher</c>: that
///     one is Velopack's install folder, replaced whole on install and removed on uninstall, and
///     anything kept there would go with it (arch-test enforced, DataFolderTests).
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiuScoresWatcher");

    /// <summary>Rolling daily logs, seven kept.</summary>
    public static string Logs { get; } = Path.Combine(Root, "logs");

    /// <summary>Result screens the reader could not parse, for the "send to the developer" button.</summary>
    public static string Failed { get; } = Path.Combine(Root, "failed");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    /// <summary>The personal token, DPAPI-encrypted for the Windows account; never inside the settings file.</summary>
    public static string TokenFile { get; } = Path.Combine(Root, "token.bin");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Failed);
    }
}

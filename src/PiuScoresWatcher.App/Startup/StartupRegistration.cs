using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PiuScoresWatcher.App.Updates;

namespace PiuScoresWatcher.App.Startup;

/// <summary>
///     Start with Windows: the per-user Run key, pointing at the installed watcher (D40). Only an
///     installed copy registers itself — a dev build from bin/ never does — and Velopack's uninstall
///     hook removes the entry with the app.
/// </summary>
public sealed class StartupRegistration(ILogger<StartupRegistration> log)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PiuScoresWatcher";

    public void Apply(bool startWithWindows)
    {
        if (!Installation.IsInstalled)
        {
            log.LogInformation("Start with Windows applies to an installed copy; this one runs from a build folder");
            return;
        }

        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        if (startWithWindows)
            run.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            run.DeleteValue(ValueName, throwOnMissingValue: false);
        log.LogInformation("Start with Windows {State}", startWithWindows ? "on" : "off");
    }

    /// <summary>For the uninstall hook, which runs before any host exists.</summary>
    public static void Remove()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        run?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

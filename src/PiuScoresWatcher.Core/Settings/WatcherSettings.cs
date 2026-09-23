namespace PiuScoresWatcher.Core.Settings;

/// <summary>
///     The player's choices on the settings window. The API token is deliberately not here: it is
///     stored on its own, encrypted for the Windows account (DPAPI), never in the settings file.
/// </summary>
/// <param name="Mode">How result screens are seen.</param>
/// <param name="StartWithWindows">Launch at sign-in and sit in the tray; default on, because the failure mode of off is an hour of unrecorded plays.</param>
/// <param name="SteamScreenshotsFolder">The RISE screenshots folder when auto-detection guessed wrong; null means detect.</param>
/// <param name="Notifications">Which notifications show; null (a settings file from before they existed) means all of them.</param>
/// <param name="LastSeenVersion">The watcher version the last launch ran, so the first launch after an update can say so (D42).</param>
public sealed record WatcherSettings(
    CaptureMode Mode,
    bool StartWithWindows,
    string? SteamScreenshotsFolder,
    NotificationSettings? Notifications = null,
    string? LastSeenVersion = null)
{
    public static WatcherSettings Default => new(CaptureMode.Both, StartWithWindows: true, SteamScreenshotsFolder: null);

    public NotificationSettings EffectiveNotifications => Notifications ?? NotificationSettings.Default;
}

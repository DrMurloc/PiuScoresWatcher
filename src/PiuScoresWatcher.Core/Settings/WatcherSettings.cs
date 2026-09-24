using System.Text.Json.Serialization;

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
/// <param name="BulkCaptureSounds">Whether a bulk capture plays its chime, tick and low tone; on unless switched off (D50).</param>
/// <param name="Language">The language the player picked (<see cref="Languages" />); null is Machine Default, which follows Windows (D59).</param>
/// <param name="PlaySounds">Whether each play makes its sound — the chime, the low tone or the tick; on unless switched off (D63).</param>
public sealed record WatcherSettings(
    CaptureMode Mode,
    bool StartWithWindows,
    string? SteamScreenshotsFolder,
    NotificationSettings? Notifications = null,
    string? LastSeenVersion = null,
    bool BulkCaptureSounds = true,
    string? Language = null,
    bool PlaySounds = true)
{
    public static WatcherSettings Default => new(CaptureMode.Both, StartWithWindows: true, SteamScreenshotsFolder: null);

    [JsonIgnore]
    public NotificationSettings EffectiveNotifications => Notifications ?? NotificationSettings.Default;
}

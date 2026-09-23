using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.Core.Settings;

/// <summary>
///     Which notifications the player wants (D35): a master switch and one per kind, all on by
///     default. Switching them off never hides a problem — the tray's status line and the settings
///     window still say when something is wrong.
/// </summary>
public sealed record NotificationSettings(bool Enabled, bool Recorded, bool NotRecorded, bool Unreadable, bool TokenRejected, bool Updated)
{
    public static NotificationSettings Default { get; } = new(true, true, true, true, true, true);

    public bool Allows(WatcherNotice notice)
    {
        return Enabled && notice switch
        {
            WatcherNotice.Recorded => Recorded,
            WatcherNotice.NotRecorded => NotRecorded,
            WatcherNotice.Unreadable => Unreadable,
            WatcherNotice.TokenRejected => TokenRejected,
            WatcherNotice.Updated => Updated,
            _ => false
        };
    }
}

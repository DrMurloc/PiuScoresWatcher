using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.App.Notifications;

/// <summary>
///     Where every notice goes: into the status (so the tray and the settings window show it whatever
///     the notification switches say) and into the log. Windows notifications join with the toasts.
/// </summary>
public sealed class WatcherNotifier(WatcherStatus status, ILogger<WatcherNotifier> log) : INotifier
{
    public void Notify(WatcherNotice notice)
    {
        status.Record(notice);
        switch (notice)
        {
            case WatcherNotice.Recorded r:
                log.LogInformation("Recorded: {Play} on {Mix}", Copy.PlayLine(r.Play), r.Outcome.Mix);
                break;
            case WatcherNotice.NotRecorded n:
                log.LogWarning("Not recorded: {Play} — {Why}; kept at {Path}", Copy.PlayLine(n.Play), n.Outcome.Describe(), n.SavedTo);
                break;
            case WatcherNotice.TokenRejected:
                log.LogWarning("The PIU Scores token was rejected; plays are kept, not posted, until it is replaced");
                break;
            case WatcherNotice.Unreadable u:
                log.LogWarning("A result screen could not be read ({Reason}); kept at {Path}", u.Reason, u.SavedTo);
                break;
            case WatcherNotice.Updated updated:
                log.LogInformation("Updated to {Version}", updated.Version);
                break;
        }
    }
}

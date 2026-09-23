using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.App.Notifications;

/// <summary>Tells the player through the log until the toasts (and the owner's copy for them) arrive with the settings window.</summary>
public sealed class LogNotifier(ILogger<LogNotifier> log) : INotifier
{
    public void Notify(WatcherNotice notice)
    {
        switch (notice)
        {
            case WatcherNotice.Recorded r:
                log.LogInformation("Recorded: {Song} {Type} {Level} — {Score} on {Mix}", r.Play.SongName, r.Play.ChartType, r.Play.Level, r.Play.Score, r.Outcome.Mix);
                break;
            case WatcherNotice.NotRecorded n:
                log.LogWarning("Not recorded: {Song} {Type} {Level} — {Why}", n.Play.SongName, n.Play.ChartType, n.Play.Level, n.Outcome.Describe());
                break;
            case WatcherNotice.TokenRejected:
                log.LogWarning("The PIU Scores token was rejected; nothing posts until it is replaced");
                break;
            case WatcherNotice.Unreadable u:
                log.LogWarning("A result screen could not be read ({Reason}); kept at {Path}", u.Reason, u.SavedTo);
                break;
        }
    }
}

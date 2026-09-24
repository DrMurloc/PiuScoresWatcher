using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Notifications;

/// <summary>What a click on a notification, or on one of its buttons, asks the running watcher to open.</summary>
public static class ToastAction
{
    public const string Key = "action";
    public const string Path = "path";
    public const string Version = "version";
    public const string Settings = "settings";
    public const string Review = "review";
    public const string Release = "release";
    public const string Site = "site";
}

/// <summary>
///     Where every notice goes: into the status, so the tray and the settings window show it whatever
///     the switches say; into the log; and, when its switch is on (D35), onto the screen as a Windows
///     notification, which never takes focus from the game. The token's notification shows once until the
///     token works again, however many plays it costs meanwhile.
/// </summary>
public sealed class WatcherNotifier : INotifier
{
    private readonly WatcherStatus _status;
    private readonly ISettingsStore _settings;
    private readonly ILogger<WatcherNotifier> _log;
    private int _toldAboutTheToken;

    public WatcherNotifier(WatcherStatus status, ISettingsStore settings, ILogger<WatcherNotifier> log)
    {
        _status = status;
        _settings = settings;
        _log = log;
        _status.Changed += (_, _) =>
        {
            // connected again: the next token problem is news
            if (_status.HasToken && !_status.TokenRejected)
                Interlocked.Exchange(ref _toldAboutTheToken, 0);
        };
    }

    public void Notify(WatcherNotice notice)
    {
        _status.Record(notice);
        Log(notice);
        if (!_settings.Load().EffectiveNotifications.Allows(notice))
            return;
        if (IsAboutTheToken(notice) && Interlocked.Exchange(ref _toldAboutTheToken, 1) == 1)
            return;

        try
        {
            Toast(notice)?.Show();
        }
        catch (Exception failure)
        {
            // Notifications turned off for the app in Windows, or no notification platform at all: the
            // status line and the settings window still say it.
            _log.LogWarning(failure, "A notification could not be shown");
        }
    }

    private static bool IsAboutTheToken(WatcherNotice notice)
    {
        return notice is WatcherNotice.TokenRejected or WatcherNotice.NotRecorded { Outcome.IsAboutTheToken: true };
    }

    private static ToastContentBuilder? Toast(WatcherNotice notice)
    {
        return notice switch
        {
            WatcherNotice.Recorded recorded => Opening(ToastAction.Settings)
                .AddText(Copy.RecordedTitle)
                .AddText(Copy.PlayLine(recorded.Play)),
            WatcherNotice.NotRecorded { Outcome: PostOutcome.NotConnected } => Token(Copy.NotConnectedTitle, Copy.NotConnectedBody),
            WatcherNotice.NotRecorded { Outcome: PostOutcome.Unauthorized } or WatcherNotice.TokenRejected =>
                Token(Copy.TokenRejectedTitle, Copy.TokenRejectedBody),
            WatcherNotice.NotRecorded notRecorded => Reviewing(notRecorded.SavedTo)
                .AddText(Copy.NotRecordedTitle)
                .AddText(Copy.NotRecordedBody(notRecorded.Outcome))
                .AddButton(ReviewButton(notRecorded.SavedTo)),
            WatcherNotice.Unreadable unreadable => Reviewing(unreadable.SavedTo)
                .AddText(Copy.UnreadableTitle)
                .AddText(Copy.UnreadableBody)
                .AddButton(ReviewButton(unreadable.SavedTo))
                .AddButton(new ToastButton().SetContent(Copy.Ignore).SetDismissActivation()),
            WatcherNotice.BulkCaptureFinished finished => Finished(finished.Tally),
            WatcherNotice.Updated updated => Opening(ToastAction.Release).AddArgument(ToastAction.Version, updated.Version)
                .AddText(Copy.UpdatedTitle(updated.Version))
                .AddButton(new ToastButton().SetContent(Copy.WhatChanged)
                    .AddArgument(ToastAction.Key, ToastAction.Release).AddArgument(ToastAction.Version, updated.Version)),
            _ => null
        };
    }

    /// <summary>The run's one summary (D50): Review only when something is waiting there.</summary>
    private static ToastContentBuilder Finished(BulkTally tally)
    {
        var toast = Opening(ToastAction.Settings)
            .AddText(Copy.BulkFinishedTitle)
            .AddText(Copy.BulkSummary(tally));
        if (tally.NotSent > 0)
            toast.AddButton(new ToastButton().SetContent(Copy.Review).AddArgument(ToastAction.Key, ToastAction.Review));
        return toast.AddButton(new ToastButton().SetContent(Copy.TrayOpenSite).AddArgument(ToastAction.Key, ToastAction.Site));
    }

    private static ToastContentBuilder Opening(string action)
    {
        return new ToastContentBuilder().AddArgument(ToastAction.Key, action);
    }

    private static ToastContentBuilder Reviewing(string savedTo)
    {
        return Opening(ToastAction.Review).AddArgument(ToastAction.Path, savedTo);
    }

    private static ToastButton ReviewButton(string savedTo)
    {
        return new ToastButton().SetContent(Copy.Review).AddArgument(ToastAction.Key, ToastAction.Review).AddArgument(ToastAction.Path, savedTo);
    }

    private static ToastContentBuilder Token(string title, string body)
    {
        return Opening(ToastAction.Settings)
            .AddText(title)
            .AddText(body)
            .AddButton(new ToastButton().SetContent(Copy.OpenSettings).AddArgument(ToastAction.Key, ToastAction.Settings));
    }

    private void Log(WatcherNotice notice)
    {
        switch (notice)
        {
            case WatcherNotice.Recorded r:
                _log.LogInformation("Recorded: {Play} on {Mix}", Copy.PlayLine(r.Play), r.Outcome.Mix);
                break;
            case WatcherNotice.NotRecorded n:
                _log.LogWarning("Not recorded: {Play} — {Why}; kept at {Path}", Copy.PlayLine(n.Play), n.Outcome.Describe(), n.SavedTo);
                break;
            case WatcherNotice.TokenRejected:
                _log.LogWarning("The stored PIU Scores token was refused at start-up; plays are kept, not posted, until it is replaced");
                break;
            case WatcherNotice.Unreadable u:
                _log.LogWarning("A result screen could not be read ({Reason}); kept at {Path}", u.Reason, u.SavedTo);
                break;
            case WatcherNotice.Updated updated:
                _log.LogInformation("Updated to {Version}", updated.Version);
                break;
            case WatcherNotice.BulkCaptureFinished finished:
                _log.LogInformation("Bulk capture finished: {Summary}", Copy.BulkSummary(finished.Tally));
                break;
        }
    }
}

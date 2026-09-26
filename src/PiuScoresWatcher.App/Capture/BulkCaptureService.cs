using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Sounds;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Capture;

/// <summary>What getting ready for a run found: the chart list and the player's bests, or why not.</summary>
public abstract record BulkPreparation
{
    public sealed record Ready(SongCatalog Catalog, IReadOnlyList<StoredBest> Bests) : BulkPreparation
    {
        public int StoredBestCount => Bests.Count(best => best.Score is not null);
    }

    public sealed record NotConnected : BulkPreparation;

    public sealed record CouldNotLoad(string Why) : BulkPreparation;
}

/// <summary>
///     The bulk capture run the player started (D45-D52): prepared from PIU Scores (the chart list and
///     their bests), fed every frame the capture loop sees while it runs, heard through the sounds, and
///     ended — from the tray or settings, when a result screen appears, when RISE closes, when the song
///     list has been gone for half a minute, or when it never shows in ten minutes — with one summary.
/// </summary>
public sealed class BulkCaptureService : IDisposable
{
    public static readonly TimeSpan ListGone = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ListNeverShown = TimeSpan.FromMinutes(10);

    private readonly SongCatalogs _catalogs;
    private readonly IClock _clock;
    private readonly Connection _connection;
    private readonly IFailedScreenStore _failed;
    private readonly IGameSession _game;
    private readonly object _gate = new();
    private readonly ILogger<BulkCaptureService> _log;
    private readonly INotifier _notifier;
    private readonly IPlaysClient _site;
    private readonly CaptureSounds _sounds;
    private readonly WatcherStatus _status;
    private readonly ITitleReader _titles;
    private readonly Timer _watchdog;
    private readonly WindowCaptureSource _window;
    private BulkCaptureRun? _run;

    public BulkCaptureService(IPlaysClient site, SongCatalogs catalogs, Connection connection, WatcherStatus status, ITitleReader titles,
        IFailedScreenStore failed, IClock clock, INotifier notifier, CaptureSounds sounds, WindowCaptureSource window, IGameSession game,
        ILogger<BulkCaptureService> log)
    {
        _site = site;
        _catalogs = catalogs;
        _connection = connection;
        _status = status;
        _titles = titles;
        _failed = failed;
        _clock = clock;
        _notifier = notifier;
        _sounds = sounds;
        _window = window;
        _game = game;
        _log = log;
        _watchdog = new Timer(_ => CheckEnd(), null, Timeout.Infinite, Timeout.Infinite);
        _game.RunningChanged += (_, running) =>
        {
            if (!running)
                Stop("RISE closed");
        };
    }

    /// <summary>A run started or stopped: the capture loop restarts its sources, since a run looks at the window whatever the mode.</summary>
    public event EventHandler? RunningChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _run is not null;
        }
    }

    public void Dispose()
    {
        _watchdog.Dispose();
    }

    /// <summary>The start window's first step: who the player is, the chart list, and their bests on PIU Scores.</summary>
    public async Task<BulkPreparation> PrepareAsync(CancellationToken cancellationToken)
    {
        // a token stored while the site was out of reach: ask again who it belongs to
        if (_status.Player is null && _status.HasToken && !_status.TokenRejected)
            await _connection.CheckStoredAsync(cancellationToken);
        if (!_status.HasToken || _status.TokenRejected)
            return new BulkPreparation.NotConnected();
        if (_status.Player is not { } player)
            return new BulkPreparation.CouldNotLoad("who the token belongs to");
        if (!await _catalogs.RefreshAsync(RiseMix.Rise, cancellationToken) || _catalogs.For(RiseMix.Rise) is not { } catalog)
            return new BulkPreparation.CouldNotLoad("the chart list");
        return await _site.GetBestsAsync(player.UserId, RiseMix.Rise, cancellationToken) switch
        {
            SiteResult<IReadOnlyList<StoredBest>>.Ok ok => new BulkPreparation.Ready(catalog, ok.Value),
            SiteResult<IReadOnlyList<StoredBest>>.Unauthorized => new BulkPreparation.NotConnected(),
            SiteResult<IReadOnlyList<StoredBest>>.Failed failed => new BulkPreparation.CouldNotLoad(failed.Message),
            _ => new BulkPreparation.CouldNotLoad("an answer the watcher did not expect")
        };
    }

    public void Start(BulkPreparation.Ready ready)
    {
        lock (_gate)
        {
            if (_run is not null)
                return;
            _run = new BulkCaptureRun(new SongListReader(), _titles, _site, _failed, _clock, ready.Catalog, ready.Bests);
        }

        _window.Fast = true;
        _watchdog.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _status.BulkProgress(BulkTally.None);
        _log.LogInformation("Bulk capture started: {Charts}, {Bests} bests on PIU Scores", ready.Catalog, ready.StoredBestCount);
        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A frame for the run; null when none is running. <see cref="BulkOutcome.NotTheList" /> leaves the frame to the result pipeline.</summary>
    public async Task<BulkOutcome?> HandleAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        BulkCaptureRun? run;
        lock (_gate)
            run = _run;
        if (run is null)
            return null;

        var outcome = await run.HandleAsync(frame, cancellationToken);
        bool stillRunning;
        lock (_gate)
            stillRunning = ReferenceEquals(_run, run);
        _sounds.Play(outcome);
        switch (outcome)
        {
            case BulkOutcome.Sent sent:
                _log.LogInformation("Bulk capture sent {Song} {Type} {Level} {Score}", sent.Play.SongName, sent.Play.ChartType, sent.Play.Level,
                    sent.Play.Score);
                break;
            case BulkOutcome.AlreadyThere already:
                _log.LogInformation("Bulk capture: {Song} {Type} {Level} already on PIU Scores", already.SongName, already.ChartType, already.Level);
                break;
            case BulkOutcome.Unreadable kept:
                LogKept(kept, "");
                break;
            case BulkOutcome.NotTheList { Kept: { } kept }:
                LogKept(kept, " as the list was left");
                break;
            case BulkOutcome.NotRecorded notRecorded:
                _log.LogWarning("Bulk capture: {Song} not recorded — {Why} ({Path})", notRecorded.Play.SongName, notRecorded.Outcome.Describe(),
                    notRecorded.SavedTo);
                break;
        }

        // a run stopped while this frame was in flight has already given its summary
        if (stillRunning && outcome is not (BulkOutcome.NotTheList { Kept: null } or BulkOutcome.Waiting))
            _status.BulkProgress(run.Tally);
        return outcome;
    }

    private void LogKept(BulkOutcome.Unreadable kept, string when)
    {
        if (kept.SavedTo is null)
            _log.LogWarning("Bulk capture kept a song list{When}: {Because} — {Reason} (kept earlier in this run)", when, kept.Because, kept.Reason);
        else
            _log.LogWarning("Bulk capture kept a song list{When}: {Because} — {Reason} ({Path})", when, kept.Because, kept.Reason, kept.SavedTo);
    }

    /// <summary>Ends the run, if one is on, with its one summary (D50).</summary>
    public void Stop(string why)
    {
        BulkCaptureRun? run;
        lock (_gate)
        {
            run = _run;
            _run = null;
        }

        if (run is null)
            return;
        _window.Fast = false;
        _watchdog.Change(Timeout.Infinite, Timeout.Infinite);
        // a chart whose title was still being read again is kept as the run last saw it (D69)
        if (run.End() is { } kept)
            LogKept(kept, " as the run ended");
        _log.LogInformation("Bulk capture ended ({Why}): {Tally}", why, run.Tally);
        _status.BulkFinished(run.Tally);
        _notifier.Notify(new WatcherNotice.BulkCaptureFinished(run.Tally));
        RunningChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Every five seconds while a run is on: has the song list gone, or never come?</summary>
    private void CheckEnd()
    {
        BulkCaptureRun? run;
        lock (_gate)
            run = _run;
        if (run is null)
            return;
        var now = _clock.Now;
        if (run.ListLastSeen is { } seen ? now - seen >= ListGone : now - run.StartedAt >= ListNeverShown)
            Stop(run.ListLastSeen is null ? "the song list never showed" : "the song list has been gone for half a minute");
    }
}

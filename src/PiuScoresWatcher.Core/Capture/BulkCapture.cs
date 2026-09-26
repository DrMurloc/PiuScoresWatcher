using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.Core.Capture;

/// <summary>What one frame did to a bulk capture run; the App plays the sound each one calls for (D50).</summary>
[ExcludeFromCodeCoverage]
public abstract record BulkOutcome
{
    /// <summary>
    ///     Not Warm Up's song list: the game, a menu, a result screen. <paramref name="Kept" /> is the chart the list was
    ///     left on while its title still named nothing, kept as the run last saw it (D69); the frame itself goes on to the
    ///     result pipeline either way.
    /// </summary>
    public sealed record NotTheList(Unreadable? Kept = null) : BulkOutcome;

    /// <summary>
    ///     The list, but not still for long enough yet, a title being read again while the panel holds (D69), a chart
    ///     already handled while it stays lit, or a chart lit that can't be placed (D72).
    /// </summary>
    public sealed record Waiting : BulkOutcome;

    /// <summary>The lit chart has no best.</summary>
    public sealed record NoBest : BulkOutcome;

    /// <summary>Sent to PIU Scores and recorded — the chime.</summary>
    public sealed record Sent(ObservedPlay Play) : BulkOutcome;

    /// <summary>PIU Scores already has this best or a higher one — the tick.</summary>
    public sealed record AlreadyThere(string SongName, ChartType ChartType, int Level) : BulkOutcome;

    /// <summary>
    ///     Could not be read, named no chart the list has, or lit a chart that can't be placed (D72): kept for review — the
    ///     low tone. <paramref name="SavedTo" /> is null for a chart this run kept already, which is heard again but not
    ///     kept twice (D69).
    /// </summary>
    public sealed record Unreadable(KeptBecause Because, string Reason, string? SavedTo) : BulkOutcome;

    /// <summary>Read and sent, and PIU Scores did not record it: kept for review — the low tone.</summary>
    public sealed record NotRecorded(ObservedPlay Play, PostOutcome Outcome, string SavedTo) : BulkOutcome;
}

/// <summary>A run's count so far: what the tray, the settings window and the summary say (D50).</summary>
[ExcludeFromCodeCoverage]
public sealed record BulkTally(int Sent, int Already, int Unreadable, int NotRecorded)
{
    public static BulkTally None { get; } = new(0, 0, 0, 0);

    public int NotSent => Unreadable + NotRecorded;
}

/// <summary>
///     One bulk capture run over Warm Up's song list (D45-D52). Each lit chart is acted on once the panel
///     has read the same for <see cref="Settle" /> (D48) — a Steam screenshot is already still — and once
///     per arrival: the best is matched to the chart list (D49), compared with the player's stored best,
///     and sent only when it is higher; anything that cannot be read is kept for review, never sent.
///     A title that names no chart is read again, frame by frame, while the panel holds — a title too long for its
///     box scrolls through it, and each frame shows another piece — for up to <see cref="TitleRetry" />, and only then
///     kept; a chart is kept once in a run however often the player comes back to it (D69). The list with a chart lit
///     that can't be placed is kept the first time it stays so for <see cref="UnplacedFor" />, and not again in the run
///     (D72).
/// </summary>
public sealed class BulkCaptureRun
{
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    /// <summary>How long a panel whose title names no chart is read again before the chart is kept (D69).</summary>
    public static readonly TimeSpan TitleRetry = TimeSpan.FromSeconds(2);

    /// <summary>How long the window shows the list with a chart it can't place before a frame of it is kept (D72).</summary>
    public static readonly TimeSpan UnplacedFor = TimeSpan.FromSeconds(2);

    /// <summary>The list with a chart it can't place, among the charts a run keeps: one entry, so it is kept once (D72).</summary>
    private static readonly Fingerprint UnplacedList = new(SongListStatus.Unplaced, null, null, null, null, 0);

    private readonly Dictionary<Guid, StoredBest> _bests;
    private readonly SongCatalog _catalog;
    private readonly IClock _clock;
    private readonly IFailedScreenStore _failed;

    /// <summary>The count and the charts kept, which <see cref="End" /> can reach from another thread while a frame is in flight.</summary>
    private readonly Lock _gate = new();

    private readonly HashSet<Fingerprint> _kept = [];
    private readonly SongListReader _reader;
    private readonly IPlaysClient _site;
    private readonly ITitleReader _titles;
    private Fingerprint? _acted;
    private (Fingerprint Fingerprint, DateTimeOffset Since)? _pending;
    private Unnamed? _unnamed;

    /// <summary>Since when the window has shown the list with a chart it can't place, frame after frame.</summary>
    private DateTimeOffset? _unplacedSince;

    public BulkCaptureRun(SongListReader reader, ITitleReader titles, IPlaysClient site, IFailedScreenStore failed, IClock clock,
        SongCatalog catalog, IEnumerable<StoredBest> bests)
    {
        _reader = reader;
        _titles = titles;
        _site = site;
        _failed = failed;
        _clock = clock;
        _catalog = catalog;
        _bests = bests.GroupBy(best => best.ChartId).ToDictionary(group => group.Key, group => group.First());
        StartedAt = clock.Now;
    }

    public DateTimeOffset StartedAt { get; }

    /// <summary>When the song list was last on screen; null until it first is.</summary>
    public DateTimeOffset? ListLastSeen { get; private set; }

    public BulkTally Tally { get; private set; } = BulkTally.None;

    /// <summary>How many of the player's bests PIU Scores held when the run started, for the start window (D51).</summary>
    public int StoredBestCount => _bests.Values.Count(best => best.Score is not null);

    public async Task<BulkOutcome> HandleAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        var fromWindow = frame.Source != CaptureSource.SteamScreenshot;
        var reading = _reader.Read(frame.Image);
        if (fromWindow && reading?.Status != SongListStatus.Unplaced)
            _unplacedSince = null;
        if (reading is null)
            return new BulkOutcome.NotTheList(KeepUnnamed());
        ListLastSeen = _clock.Now;
        if (reading.Status == SongListStatus.Unplaced)
            return Unplaced(frame, reading.Reason ?? "the song list with a chart lit that can't be placed", fromWindow);

        // one action per arrival: the chart that was acted on stays quiet until the panel shows something else
        var fingerprint = new Fingerprint(reading.Status, reading.ChartType, reading.Level, reading.Score, reading.Grade, reading.Jacket);
        if (fingerprint == _acted)
            return new BulkOutcome.Waiting();
        _acted = null;

        // the window's panel moved on while the last chart's title still named nothing: that chart is kept as it was last
        // seen, and this frame starts the next one's half second (a screenshot is its own frame, and leaves it be)
        if (fromWindow && _unnamed is { } unnamed && unnamed.Fingerprint != fingerprint)
        {
            var kept = KeepUnnamed()!;
            HasSettled(fingerprint);
            return kept;
        }

        var retrying = _unnamed?.Fingerprint == fingerprint;
        if (fromWindow && !retrying && !HasSettled(fingerprint))
            return new BulkOutcome.Waiting();
        _pending = null;

        return reading.Status switch
        {
            SongListStatus.NoBest => Acted(fingerprint, new BulkOutcome.NoBest()),
            SongListStatus.Best => await CaptureAsync(frame, reading, fingerprint, cancellationToken),
            SongListStatus.GradeDisagrees => Acted(fingerprint,
                Keep(frame, fingerprint, KeptBecause.GradeDisagrees, reading.Reason ?? "the grade disagrees with the score")),
            _ => Acted(fingerprint, Keep(frame, fingerprint, KeptBecause.ListUnreadable, reading.Reason ?? "unreadable"))
        };
    }

    /// <summary>The run is over: a chart whose title still named nothing is kept as the run last saw it (D69).</summary>
    public BulkOutcome.Unreadable? End()
    {
        return KeepUnnamed();
    }

    /// <summary>
    ///     The list, with a chart lit that can't be placed (D72): from the window, kept once it has stayed so for
    ///     <see cref="UnplacedFor" />, a screenshot at once, and only the first time in the run — after that the run says
    ///     nothing more about it. The chart whose title the window was reading again is no longer the one lit, and is kept
    ///     as it was last seen.
    /// </summary>
    private BulkOutcome Unplaced(CapturedFrame frame, string reason, bool fromWindow)
    {
        var now = _clock.Now;
        if (fromWindow && KeepUnnamed() is { } unnamed)
        {
            _unplacedSince = now;
            return unnamed;
        }

        lock (_gate)
            if (_kept.Contains(UnplacedList))
                return new BulkOutcome.Waiting();
        if (fromWindow)
        {
            _unplacedSince ??= now;
            if (now - _unplacedSince < UnplacedFor)
                return new BulkOutcome.Waiting();
        }

        return Keep(frame, UnplacedList, KeptBecause.ChartUnplaced, reason);
    }

    private bool HasSettled(Fingerprint fingerprint)
    {
        var now = _clock.Now;
        if (_pending is not { } pending || pending.Fingerprint != fingerprint)
        {
            _pending = (fingerprint, now);
            return false;
        }

        return now - pending.Since >= Settle;
    }

    private async Task<BulkOutcome> CaptureAsync(CapturedFrame frame, SongListReading reading, Fingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var type = reading.ChartType!.Value;
        var level = reading.Level!.Value;
        var score = reading.Score!.Value;
        var choice = await _titles.ChooseAsync(frame.Image, reading.Titles, _catalog, type, level, null, cancellationToken);
        if (choice.Match is not CatalogMatch.Found { Chart: var chart })
            return Unmatched(frame, fingerprint, choice, type, level);

        if (_unnamed?.Fingerprint == fingerprint)
            _unnamed = null;
        _acted = fingerprint;
        if (_bests.TryGetValue(chart.Id, out var stored) && stored is { IsBroken: false, Score: { } storedScore } && storedScore >= score)
        {
            Count(tally => tally with { Already = tally.Already + 1 });
            return new BulkOutcome.AlreadyThere(chart.SongName, type, level);
        }

        var play = ObservedPlay.Captured(RiseMix.Rise, chart.SongName, type, level, score, frame.SeenAt);
        var outcome = await _site.PostAsync(play, CaptureSources.SongList, cancellationToken);
        if (outcome is PostOutcome.Recorded)
        {
            _bests[chart.Id] = new StoredBest(chart.Id, score, false);
            Count(tally => tally with { Sent = tally.Sent + 1 });
            return new BulkOutcome.Sent(play);
        }

        var savedTo = _failed.Save(frame, Kept.Because(outcome), outcome.Describe(), null);
        Count(tally => tally with { NotRecorded = tally.NotRecorded + 1 });
        return new BulkOutcome.NotRecorded(play, outcome, savedTo);
    }

    /// <summary>
    ///     A title that named no chart on this frame. A screenshot is final and is kept; the window's next frame shows the
    ///     title anew, so the chart waits for it — until <see cref="TitleRetry" /> has gone by since its first try (D69).
    /// </summary>
    private BulkOutcome Unmatched(CapturedFrame frame, Fingerprint fingerprint, TitleChoice choice, ChartType type, int level)
    {
        var (because, reason) = choice.Match is CatalogMatch.Unlisted unlisted
            ? (KeptBecause.ChartUnlisted, $"the title read as {choice.Described} names {unlisted.SongName}, which PIU Scores doesn't list at {type} {level}")
            : (KeptBecause.TitleUnmatched, $"the title read as {choice.Described} names no {type} {level} on PIU Scores");
        var retrying = _unnamed?.Fingerprint == fingerprint;
        var since = retrying ? _unnamed!.Since : _clock.Now;
        if (frame.Source == CaptureSource.SteamScreenshot || _clock.Now - since >= TitleRetry)
        {
            if (retrying)
                _unnamed = null;
            return Acted(fingerprint, Keep(frame, fingerprint, because, reason));
        }

        _unnamed = new Unnamed(fingerprint, since, frame, because, reason);
        return new BulkOutcome.Waiting();
    }

    /// <summary>The chart whose title never named one, kept as it was last seen; null when there is none.</summary>
    private BulkOutcome.Unreadable? KeepUnnamed()
    {
        if (Interlocked.Exchange(ref _unnamed, null) is not { } unnamed)
            return null;
        _acted = unnamed.Fingerprint;
        return Keep(unnamed.Frame, unnamed.Fingerprint, unnamed.Because, unnamed.Reason);
    }

    private BulkOutcome Acted(Fingerprint fingerprint, BulkOutcome outcome)
    {
        _acted = fingerprint;
        return outcome;
    }

    /// <summary>Kept for review once per chart in a run: coming back to it plays the low tone again and keeps nothing more (D69).</summary>
    private BulkOutcome.Unreadable Keep(CapturedFrame frame, Fingerprint fingerprint, KeptBecause because, string reason)
    {
        lock (_gate)
        {
            if (!_kept.Add(fingerprint))
                return new BulkOutcome.Unreadable(because, reason, null);
            Tally = Tally with { Unreadable = Tally.Unreadable + 1 };
        }

        return new BulkOutcome.Unreadable(because, reason, _failed.Save(frame, because, reason, null));
    }

    private void Count(Func<BulkTally, BulkTally> change)
    {
        lock (_gate)
            Tally = change(Tally);
    }

    /// <summary>
    ///     What the panel showed, and whose jacket is lit in the list; a change of any part is a new arrival. The jacket
    ///     is there because two songs in a row can show the same level, score and grade — every 1,000,000 is an SSS.
    /// </summary>
    private sealed record Fingerprint(SongListStatus Status, ChartType? ChartType, int? Level, int? Score, string? Grade, ulong Jacket);

    /// <summary>A chart whose title has named nothing since <paramref name="Since" />, and how it was last seen.</summary>
    private sealed record Unnamed(Fingerprint Fingerprint, DateTimeOffset Since, CapturedFrame Frame, KeptBecause Because, string Reason);
}

using System.Numerics;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.Core.Capture;

/// <summary>
///     What one frame did to a bulk capture run; the App plays the sound each one calls for (D50). Whatever became of the lit
///     chart, <see cref="PerfectGames" /> is what the other rows' Perfect Games came to on this frame (D84-D86).
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record BulkOutcome
{
    /// <summary>The rows' Perfect Games acted on with this frame; only one sent is heard (D86, D87).</summary>
    public IReadOnlyList<PerfectGameOutcome> PerfectGames { get; init; } = [];

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

/// <summary>
///     What became of one Perfect Game a row of the song list showed (D84-D86). Only <see cref="Sent" /> is heard, counted
///     and told; the rest are for the log.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record PerfectGameOutcome
{
    /// <summary>Sent at 1,000,000 and recorded — Level up.</summary>
    public sealed record Sent(ObservedPlay Play) : PerfectGameOutcome;

    /// <summary>PIU Scores already has the million.</summary>
    public sealed record AlreadyThere(string SongName, ChartType ChartType, int Level) : PerfectGameOutcome;

    /// <summary>The row's title named no chart at its Perfect Games' levels, or its song has none at this one on PIU Scores: not sent.</summary>
    public sealed record Unplaced(int Row, ChartType ChartType, int Level, string Reason) : PerfectGameOutcome;

    /// <summary>Sent, and PIU Scores didn't record it; one lost to the network or a rate limit is tried again when its row next settles.</summary>
    public sealed record NotRecorded(ObservedPlay Play, PostOutcome Outcome) : PerfectGameOutcome;
}

/// <summary>
///     A run's count so far: what the tray, the settings window and the summary say (D50). <see cref="PerfectGames" /> are
///     the Perfect Games among <see cref="Sent" />, from the rows and the lit chart alike (D88).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BulkTally(int Sent, int Already, int Unreadable, int NotRecorded, int PerfectGames = 0)
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
///     The other rows' Perfect Games go up too, highlighted or not (D84): once the rows have held still for
///     <see cref="Settle" />, they are taken up with their frame, and on the first frame the lit chart isn't making its
///     sound or reading its title again, whatever that frame shows, each row with one has its title read and each Perfect
///     Game PIU Scores has less than a million on is sent, so moving on at the sound loses nothing (D87). A row is acted on
///     once a run, known again by its jacket and its Perfect Games' levels, and nothing about it is heard or kept but a
///     Perfect Game sent (D85, D86).
/// </summary>
public sealed class BulkCaptureRun
{
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    /// <summary>How far a row's jacket may print from the one acted on and still be that row: the same song differs by a bit or none, two songs by 18 or more (D48, D85).</summary>
    public const int JacketBits = 4;

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

    /// <summary>The rows acted on this run (D85).</summary>
    private readonly List<RowKey> _rowsDone = [];

    private readonly IPlaysClient _site;
    private readonly ITitleReader _titles;
    private Fingerprint? _acted;
    private (Fingerprint Fingerprint, DateTimeOffset Since)? _pending;

    /// <summary>The rows as last taken up, and as they have stood since when: a change of any row is the list moving (D85).</summary>
    private string? _rowsActed;

    /// <summary>Rows taken up with their frame, waiting for the lit chart to be done with its sound or its title (D85, D87).</summary>
    private readonly Queue<(CapturedFrame Frame, SongListReading Reading)> _rowsHeld = new();

    private (string Rows, DateTimeOffset Since)? _rowsPending;
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
        var outcome = await OutcomeAsync(frame, reading, fromWindow, cancellationToken);

        // From the window the rows taken up wait while the lit chart makes its sound, so both are heard, and while it reads
        // its title again, so their reads and posts don't eat its two seconds (D69, D87). Any other frame acts on them,
        // whatever it shows (the list moved on at the sound, or gone): a row is known by its jacket, not its place (D85).
        var litFirst = fromWindow && reading is not null && (outcome is not (BulkOutcome.Waiting or BulkOutcome.NoBest) || _unnamed is not null);
        if (_rowsHeld.Count == 0 || litFirst)
            return outcome;
        var perfectGames = await PerfectGamesAsync(cancellationToken);
        return perfectGames.Count == 0 ? outcome : outcome with { PerfectGames = perfectGames };
    }

    /// <summary>What a frame does apart from the rows' Perfect Games: the list gone, a chart lit that can't be placed, or the lit chart.</summary>
    private async Task<BulkOutcome> OutcomeAsync(CapturedFrame frame, SongListReading? reading, bool fromWindow, CancellationToken cancellationToken)
    {
        if (reading is null)
            return new BulkOutcome.NotTheList(KeepUnnamed());
        ListLastSeen = _clock.Now;
        if (reading.Status == SongListStatus.Unplaced)
            return Unplaced(frame, reading.Reason ?? "the song list with a chart lit that can't be placed", fromWindow);
        if (RowsToActOn(reading.ChartType!.Value, reading.Rows, fromWindow) is { } rows)
        {
            _rowsActed = rows;
            _rowsHeld.Enqueue((frame, reading));
        }

        return await LitChartAsync(frame, reading, fromWindow, cancellationToken);
    }

    /// <summary>The lit chart's part of a frame of the list: the half-second wait, the capture, the title read again (D48-D52, D69).</summary>
    private async Task<BulkOutcome> LitChartAsync(CapturedFrame frame, SongListReading reading, bool fromWindow, CancellationToken cancellationToken)
    {
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
    ///     The rows as they stand, when they are to be acted on with this frame: a row holds a Perfect Game, they have held
    ///     still for <see cref="Settle" /> (a screenshot is already still), and they were not acted on as they stand (D85).
    ///     As they stand is on this tab, with these Perfect Games: after TAB the same songs, marks alike, are other charts.
    /// </summary>
    private string? RowsToActOn(ChartType type, IReadOnlyList<SongListRow> rows, bool fromWindow)
    {
        if (!rows.Any(row => row.PerfectGames.Count > 0))
        {
            // rows seen before are taken up again when the list comes back to them, for a post that was lost (D86)
            _rowsActed = null;
            _rowsPending = null;
            return null;
        }

        var print = $"{type} " + string.Join(" ", rows.Select(row =>
            $"{row.Row}:{row.Jacket:x16}:{(row.Marks is null ? "?" : string.Concat(row.Marks))}:{string.Join(",", row.PerfectGames)}"));
        if (print == _rowsActed)
            return null;
        if (!fromWindow)
            return print;
        var now = _clock.Now;
        if (_rowsPending is not { } pending || pending.Rows != print)
        {
            _rowsPending = (print, now);
            return null;
        }

        if (now - pending.Since < Settle)
            return null;
        _rowsPending = null;
        return print;
    }

    /// <summary>
    ///     The rows' Perfect Games (D84-D86), from each frame the rows were taken up on: a row not acted on this run has its
    ///     title read and matched at its Perfect Games' levels, and each one PIU Scores has less than a million on is sent. A
    ///     row is done once acted on, unless a post was lost to the network or a rate limit: then it is tried again the next
    ///     time it settles in view.
    /// </summary>
    private async Task<IReadOnlyList<PerfectGameOutcome>> PerfectGamesAsync(CancellationToken cancellationToken)
    {
        List<PerfectGameOutcome> outcomes = [];
        while (_rowsHeld.TryDequeue(out var held))
        {
            var (frame, reading) = held;
            var type = reading.ChartType!.Value;
            foreach (var row in reading.Rows.Where(row => row.PerfectGames.Count > 0 && !Done(type, row)))
            {
                var choice = await _titles.ChooseSongAsync(frame.Image, row.Title, _catalog, type, row.PerfectGames, cancellationToken);
                var again = false;
                foreach (var level in row.PerfectGames)
                {
                    var outcome = await PerfectGameAsync(frame, row, choice, type, level, cancellationToken);
                    again |= outcome is PerfectGameOutcome.NotRecorded { Outcome: PostOutcome.RateLimited or PostOutcome.Failed };
                    outcomes.Add(outcome);
                }

                if (!again)
                    _rowsDone.Add(new RowKey(type, row.Jacket, row.PerfectGames));
            }
        }

        return outcomes;
    }

    private async Task<PerfectGameOutcome> PerfectGameAsync(CapturedFrame frame, SongListRow row, TitleChoice choice, ChartType type, int level,
        CancellationToken cancellationToken)
    {
        if (choice.Match is not CatalogMatch.Found { Chart.SongName: var song })
            return new PerfectGameOutcome.Unplaced(row.Row, type, level, choice.Match is CatalogMatch.Unlisted unlisted
                ? $"the title read as {choice.Described} names {unlisted.SongName}, which PIU Scores doesn't list at the row's Perfect Games"
                : $"the title read as {choice.Described} names no chart at the row's Perfect Games");
        if (_catalog.Chart(song, type, level) is not { } chart)
            return new PerfectGameOutcome.Unplaced(row.Row, type, level, $"PIU Scores doesn't list {song} at {type} {level}");
        if (_bests.TryGetValue(chart.Id, out var stored) && stored is { IsBroken: false, Score: >= PhoenixScoring.PerfectGameScore })
            return new PerfectGameOutcome.AlreadyThere(chart.SongName, type, level);

        var play = ObservedPlay.Captured(RiseMix.Rise, chart.SongName, type, level, PhoenixScoring.PerfectGameScore, frame.SeenAt);
        var outcome = await _site.PostAsync(play, CaptureSources.SongList, cancellationToken);
        if (outcome is not PostOutcome.Recorded)
            return new PerfectGameOutcome.NotRecorded(play, outcome);
        _bests[chart.Id] = new StoredBest(chart.Id, PhoenixScoring.PerfectGameScore, false);
        Count(tally => tally with { Sent = tally.Sent + 1, PerfectGames = tally.PerfectGames + 1 });
        return new PerfectGameOutcome.Sent(play);
    }

    /// <summary>A row acted on this run: seen on the same tab, with the same Perfect Games, and a jacket printing within <see cref="JacketBits" />.</summary>
    private bool Done(ChartType type, SongListRow row)
    {
        return _rowsDone.Any(done => done.Type == type && done.Levels.SequenceEqual(row.PerfectGames)
                                                       && BitOperations.PopCount(done.Jacket ^ row.Jacket) <= JacketBits);
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
            Count(tally => tally with { Sent = tally.Sent + 1, PerfectGames = tally.PerfectGames + (play.IsPerfectGame ? 1 : 0) });
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

    /// <summary>A row acted on: the tab it was seen on, its jacket's print, and its Perfect Games' levels (D85).</summary>
    private sealed record RowKey(ChartType Type, ulong Jacket, IReadOnlyList<int> Levels);
}

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
    /// <summary>Not Warm Up's song list: the game, a menu, a result screen.</summary>
    public sealed record NotTheList : BulkOutcome;

    /// <summary>The list, but not still for long enough yet, or a chart already handled while it stays lit.</summary>
    public sealed record Waiting : BulkOutcome;

    /// <summary>The lit chart has no best.</summary>
    public sealed record NoBest : BulkOutcome;

    /// <summary>Sent to PIU Scores and recorded — the chime.</summary>
    public sealed record Sent(ObservedPlay Play) : BulkOutcome;

    /// <summary>PIU Scores already has this best or a higher one — the tick.</summary>
    public sealed record AlreadyThere(string SongName, ChartType ChartType, int Level) : BulkOutcome;

    /// <summary>Could not be read, or matched no song: kept for review — the low tone.</summary>
    public sealed record Unreadable(KeptBecause Because, string Reason, string SavedTo) : BulkOutcome;

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
/// </summary>
public sealed class BulkCaptureRun
{
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private readonly Dictionary<Guid, StoredBest> _bests;
    private readonly SongCatalog _catalog;
    private readonly IClock _clock;
    private readonly IFailedScreenStore _failed;
    private readonly SongListReader _reader;
    private readonly IPlaysClient _site;
    private readonly ITitleReader _titles;
    private Fingerprint? _acted;
    private (Fingerprint Fingerprint, DateTimeOffset Since)? _pending;

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
        var reading = _reader.Read(frame.Image);
        if (reading is null)
            return new BulkOutcome.NotTheList();
        ListLastSeen = _clock.Now;

        // one action per arrival: the chart that was acted on stays quiet until the panel shows something else
        var fingerprint = new Fingerprint(reading.Status, reading.ChartType, reading.Level, reading.Score, reading.Grade);
        if (fingerprint == _acted)
            return new BulkOutcome.Waiting();
        _acted = null;
        if (frame.Source != CaptureSource.SteamScreenshot && !HasSettled(fingerprint))
            return new BulkOutcome.Waiting();
        _acted = fingerprint;
        _pending = null;

        return reading.Status switch
        {
            SongListStatus.NoBest => new BulkOutcome.NoBest(),
            SongListStatus.Best => await CaptureAsync(frame, reading, cancellationToken),
            SongListStatus.GradeDisagrees => Keep(frame, KeptBecause.GradeDisagrees, reading.Reason ?? "the grade disagrees with the score"),
            _ => Keep(frame, KeptBecause.ListUnreadable, reading.Reason ?? "unreadable")
        };
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

    private async Task<BulkOutcome> CaptureAsync(CapturedFrame frame, SongListReading reading, CancellationToken cancellationToken)
    {
        var level = reading.Level!.Value;
        var score = reading.Score!.Value;
        var choice = await _titles.ChooseAsync(frame.Image, reading.TitleRegion, _catalog, reading.ChartType, level, null, cancellationToken);
        if (choice.Match is not CatalogMatch.Found { Chart: var chart })
            return Keep(frame, KeptBecause.TitleUnmatched,
                $"the title read as '{choice.Read}' names no {reading.ChartType} {level} on PIU Scores");

        if (_bests.TryGetValue(chart.Id, out var stored) && stored is { IsBroken: false, Score: { } storedScore } && storedScore >= score)
        {
            Tally = Tally with { Already = Tally.Already + 1 };
            return new BulkOutcome.AlreadyThere(chart.SongName, reading.ChartType, level);
        }

        var play = ObservedPlay.Captured(RiseMix.Rise, chart.SongName, reading.ChartType, level, score, frame.SeenAt);
        var outcome = await _site.PostAsync(play, CaptureSources.SongList, cancellationToken);
        if (outcome is PostOutcome.Recorded)
        {
            _bests[chart.Id] = new StoredBest(chart.Id, score, false);
            Tally = Tally with { Sent = Tally.Sent + 1 };
            return new BulkOutcome.Sent(play);
        }

        var savedTo = _failed.Save(frame, Kept.Because(outcome), outcome.Describe(), null);
        Tally = Tally with { NotRecorded = Tally.NotRecorded + 1 };
        return new BulkOutcome.NotRecorded(play, outcome, savedTo);
    }

    private BulkOutcome.Unreadable Keep(CapturedFrame frame, KeptBecause because, string reason)
    {
        var savedTo = _failed.Save(frame, because, reason, null);
        Tally = Tally with { Unreadable = Tally.Unreadable + 1 };
        return new BulkOutcome.Unreadable(because, reason, savedTo);
    }

    /// <summary>What the panel showed; a change of any part is a new arrival.</summary>
    private sealed record Fingerprint(SongListStatus Status, ChartType ChartType, int? Level, int? Score, string? Grade);
}

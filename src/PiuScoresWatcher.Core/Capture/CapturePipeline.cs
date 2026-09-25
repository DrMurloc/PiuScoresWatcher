using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Core.Capture;

/// <summary>What the pipeline did with one frame; the App logs it, the notifier already told the player what matters.</summary>
[ExcludeFromCodeCoverage]
public abstract record FrameOutcome
{
    /// <summary>Not a result screen: the song wheel, gameplay, a menu, a loading frame.</summary>
    public sealed record NotAResult : FrameOutcome;

    /// <summary>A result screen whose numbers have not all landed or do not agree yet; the next frame will.</summary>
    public sealed record NotYet(string Reason) : FrameOutcome;

    /// <summary>A result layout that is not one play (a Challenge aggregate).</summary>
    public sealed record NotAPlay : FrameOutcome;

    /// <summary>The same play, already handled.</summary>
    public sealed record Duplicate : FrameOutcome;

    /// <summary>Could not be turned into a play; kept for the developer.</summary>
    public sealed record Kept(string Reason, string SavedTo) : FrameOutcome;

    /// <summary>Posted, with what the site answered.</summary>
    public sealed record Posted(ObservedPlay Play, PostOutcome Outcome) : FrameOutcome;
}

/// <summary>
///     One frame in, one outcome out: detect, read, reconcile, dedupe, read the title, post, tell the
///     player. Frames from the window are cheap to retry, so one that has not settled is simply
///     "not yet"; a screenshot file is final, so one that does not reconcile is kept for review, and
///     so is any play the site does not record.
///     A window result that never reconciles is not left there (D54): once its numbers have stood still
///     for <see cref="StandStill" /> and still disagree, or the screen goes away before they ever agree, it
///     is kept for review the way a screenshot is — the Arcade Station's first 5 was misread that way and
///     only its F12 copy ever reached the player. On one visit to a result screen, the first frame that
///     reconciles speaks for it: a frame beside it that disagrees or cannot be read is the same screen
///     misread, and is dropped, not kept (owner, 2026-09-24).
///     The title is read only for a play not seen before — the window shows the same screen once a
///     second, and OCR is the expensive step — attempt by attempt until one names a chart (D55), and
///     posted in the catalog's own spelling whenever the mix's chart list is loaded and names the song
///     (D49), at the level the chart's note count says was played (D56).
/// </summary>
public sealed class CapturePipeline(
    ResultScreenDetector detector,
    ResultScreenReader reader,
    ITitleReader titles,
    IPlaysClient site,
    Deduplicator deduplicator,
    IFailedScreenStore failed,
    INotifier notifier,
    ISongCatalogs catalogs)
{
    /// <summary>How long a window result's numbers stand still, still disagreeing, before it is kept (D54).</summary>
    public static readonly TimeSpan StandStill = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     The window's current visit to a result screen, from its first frame until one that is not a result. The
    ///     screen stays up as long as the player leaves it (D11), so within a visit the play it shows is handled once,
    ///     however long it stands — the dedupe window alone let a screen left up for eleven minutes post its play
    ///     twice — and it is kept for review at most once, however many frames the video behind its numbers sends.
    /// </summary>
    private PlayKey? _handledThisVisit;

    private bool _keptThisVisit;

    /// <summary>A frame of this visit reconciled: every frame of it that does not is the same screen misread.</summary>
    private bool _settledThisVisit;

    /// <summary>The frame of this visit that is not a play yet, and since when it has read as it does.</summary>
    private Pending? _pending;

    public async Task<FrameOutcome> HandleAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        var fromWindow = frame.Source == CaptureSource.GameWindow;
        var layout = detector.Detect(frame.Image);
        if (layout is null)
        {
            if (fromWindow)
                EndVisit();
            return new FrameOutcome.NotAResult();
        }

        var reading = reader.Read(frame.Image, layout.Value);
        switch (reading.Status)
        {
            case ReadingStatus.NotAPlay:
                if (fromWindow)
                    EndVisit();
                return new FrameOutcome.NotAPlay();
            case ReadingStatus.NumbersNotShown:
                return fromWindow
                    ? new FrameOutcome.NotYet(reading.Reason ?? "the numbers have not landed")
                    : Keep(frame, KeptBecause.NumbersNotShown, reading.Reason ?? "the numbers had not landed", reading);
            case ReadingStatus.Unreadable:
                return fromWindow
                    ? Wait(frame, reading, KeptBecause.NumbersUnreadable, reading.Reason ?? "unreadable")
                    : Keep(frame, KeptBecause.NumbersUnreadable, reading.Reason ?? "unreadable", reading);
        }

        var verdict = PlayChecksum.Verify(reading);
        if (!verdict.Reconciles)
        {
            var problem = verdict.Problem ?? "the numbers do not agree";
            if (fromWindow)
                return Wait(frame, reading, KeptBecause.NumbersDisagree, problem);
            // kept already — from the window, or from a screenshot of the same screen — is kept once
            return PlayKey.Of(reading) is { } handled && !deduplicator.IsNew(handled)
                ? new FrameOutcome.Duplicate()
                : Keep(frame, KeptBecause.NumbersDisagree, problem, reading);
        }

        if (fromWindow)
        {
            // the first frame of the visit that reconciles speaks for the screen: one still waiting was it misread
            _settledThisVisit = true;
            _pending = null;
        }

        var key = PlayKey.Of(reading)!;
        if (HandledThisVisit(fromWindow, key) || !deduplicator.IsNew(key))
            return new FrameOutcome.Duplicate();

        var type = reading.ChartType!.Value;
        var level = reading.Level!.Value;
        // a broken play may not add up to the chart's notes, so only a clear one is checked against them
        int? notes = reading.IsBroken ? null : reading.Judgments!.Value.Notes;
        var choice = await titles.ChooseAsync(frame.Image, reading.Titles, catalogs.For(reading.Mix), type, level, notes, cancellationToken);
        if (choice.Read is null)
            return Keep(frame, KeptBecause.TitleUnreadable, "the song title could not be read", reading);
        if (choice.Match is CatalogMatch.Contradicted contradicted)
            return Keep(frame, KeptBecause.ChartDisagrees,
                $"{contradicted.Chart.SongName} {type} {level} has {contradicted.Chart.NoteCount} notes; the judgments add up to {contradicted.Notes}",
                reading);

        var play = ObservedPlay.From(reading, choice.Read, frame.SeenAt);
        if (choice.Match is CatalogMatch.Found found)
            play = play with { SongName = found.Chart.SongName, Level = found.Chart.Level };
        var outcome = await site.PostAsync(play, frame.Source.Token(), cancellationToken);
        Remember(frame, key);
        if (outcome is PostOutcome.Recorded recorded)
        {
            notifier.Notify(new WatcherNotice.Recorded(play, recorded));
            return new FrameOutcome.Posted(play, outcome);
        }

        // Whatever the reason, a play the site did not record is kept, so a failure never loses one silently (D38).
        var savedTo = failed.Save(frame, Kept.Because(outcome), outcome.Describe(), reading);
        notifier.Notify(new WatcherNotice.NotRecorded(play, outcome, savedTo));
        return new FrameOutcome.Posted(play, outcome);
    }

    /// <summary>
    ///     A window frame that is not a play: its numbers disagree, or a glyph would not read. Once a frame of the visit
    ///     has reconciled or been kept it is the same screen again, and is dropped. Otherwise it waits (D54) — kept once
    ///     it has read the same for <see cref="StandStill" />, or when the screen goes away, unless a frame that
    ///     reconciles arrives first.
    /// </summary>
    private FrameOutcome Wait(CapturedFrame frame, ResultScreenReading reading, KeptBecause because, string problem)
    {
        var key = PlayKey.Of(reading);
        if (_settledThisVisit || _keptThisVisit || (key is not null && !deduplicator.IsNew(key)))
            return new FrameOutcome.Duplicate();

        if (_pending is { } pending && pending.Key == key)
        {
            if (frame.SeenAt - pending.Since < StandStill)
                return new FrameOutcome.NotYet(problem);
            _pending = null;
            return Keep(frame, because, problem, reading);
        }

        // the first, or the numbers moved (a score still counting up, a misread that changes): the clock starts again
        _pending = new Pending(reading, key, frame, because, problem, frame.SeenAt);
        return new FrameOutcome.NotYet(problem);
    }

    private FrameOutcome Keep(CapturedFrame frame, KeptBecause because, string reason, ResultScreenReading reading)
    {
        if (PlayKey.Of(reading) is { } key)
            Remember(frame, key);
        if (frame.Source == CaptureSource.GameWindow)
            _keptThisVisit = true;
        var path = failed.Save(frame, because, reason, reading);
        notifier.Notify(new WatcherNotice.Unreadable(reason, path));
        return new FrameOutcome.Kept(reason, path);
    }

    /// <summary>Handled: for the dedupe window, and from the window for as long as this visit to the screen lasts.</summary>
    private void Remember(CapturedFrame frame, PlayKey key)
    {
        deduplicator.Remember(key);
        if (frame.Source == CaptureSource.GameWindow)
            _handledThisVisit = key;
    }

    private bool HandledThisVisit(bool fromWindow, PlayKey key)
    {
        return fromWindow && key == _handledThisVisit;
    }

    /// <summary>
    ///     The window left the result screen. A frame still waiting never had one that reconciled beside it, so it is
    ///     kept — unless another source already handled the play — and the next screen is a new visit.
    /// </summary>
    private void EndVisit()
    {
        if (_pending is { } pending && (pending.Key is null || deduplicator.IsNew(pending.Key)))
            Keep(pending.Frame, pending.Because, pending.Problem, pending.Reading);
        _pending = null;
        _settledThisVisit = false;
        _keptThisVisit = false;
        _handledThisVisit = null;
    }

    private sealed record Pending(
        ResultScreenReading Reading, PlayKey? Key, CapturedFrame Frame, KeptBecause Because, string Problem, DateTimeOffset Since);
}

using PiuScoresWatcher.Core.Api;
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
///     "not yet"; a screenshot file is final, so one that does not reconcile is kept for review.
///     The title is read only for a play not seen before — the window shows the same screen once a
///     second, and OCR is the expensive step.
/// </summary>
public sealed class CapturePipeline(
    ResultScreenDetector detector,
    ResultScreenReader reader,
    ITitleReader titles,
    IPlaysClient site,
    Deduplicator deduplicator,
    IFailedScreenStore failed,
    INotifier notifier)
{
    public async Task<FrameOutcome> HandleAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        var layout = detector.Detect(frame.Image);
        if (layout is null)
            return new FrameOutcome.NotAResult();

        var reading = reader.Read(frame.Image, layout.Value);
        switch (reading.Status)
        {
            case ReadingStatus.NotAPlay:
                return new FrameOutcome.NotAPlay();
            case ReadingStatus.NumbersNotShown:
                return frame.Source == CaptureSource.SteamScreenshot
                    ? Keep(frame, reading.Reason ?? "the numbers had not landed", reading)
                    : new FrameOutcome.NotYet(reading.Reason ?? "the numbers have not landed");
            case ReadingStatus.Unreadable:
                return Keep(frame, reading.Reason ?? "unreadable", reading);
        }

        var verdict = PlayChecksum.Verify(reading);
        if (!verdict.Reconciles)
            return frame.Source == CaptureSource.SteamScreenshot
                ? Keep(frame, verdict.Problem ?? "the numbers do not agree", reading)
                : new FrameOutcome.NotYet(verdict.Problem ?? "the numbers do not agree yet");

        var key = PlayKey.Of(reading)!;
        if (!deduplicator.IsNew(key))
            return new FrameOutcome.Duplicate();

        var title = await titles.ReadAsync(frame.Image, reading.TitleRegion, cancellationToken);
        if (string.IsNullOrWhiteSpace(title))
            return Keep(frame, "the song title could not be read", reading);

        var play = ObservedPlay.From(reading, title, frame.SeenAt);
        var outcome = await site.PostAsync(play, frame.Source, cancellationToken);
        deduplicator.Remember(key);
        switch (outcome)
        {
            case PostOutcome.Recorded recorded:
                notifier.Notify(new WatcherNotice.Recorded(play, recorded));
                break;
            case PostOutcome.Unauthorized:
                notifier.Notify(new WatcherNotice.TokenRejected());
                break;
            default:
                notifier.Notify(new WatcherNotice.NotRecorded(play, outcome));
                break;
        }

        return new FrameOutcome.Posted(play, outcome);
    }

    private FrameOutcome Keep(CapturedFrame frame, string reason, ResultScreenReading reading)
    {
        if (PlayKey.Of(reading) is { } key)
            deduplicator.Remember(key);
        var path = failed.Save(frame, reason, reading);
        notifier.Notify(new WatcherNotice.Unreadable(reason, path));
        return new FrameOutcome.Kept(reason, path);
    }
}

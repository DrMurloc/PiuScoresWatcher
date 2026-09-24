using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Core.Capture;

/// <summary>One frame a source saw: the pixels, which way it was seen, when, and where it came from when it was a file.</summary>
[ExcludeFromCodeCoverage]
public sealed record CapturedFrame(ScreenImage Image, CaptureSource Source, DateTimeOffset SeenAt, string? Origin);

/// <summary>
///     A way of seeing the game: the window once a second while it runs, or the Steam screenshots
///     folder. A source pushes every frame it sees; the pipeline decides what it is.
/// </summary>
public interface IScreenSource
{
    CaptureSource Kind { get; }

    /// <summary>Runs until cancelled, calling <paramref name="onFrame" /> for each frame in turn.</summary>
    Task RunAsync(Func<CapturedFrame, Task> onFrame, CancellationToken cancellationToken);
}

/// <summary>Whether the game is running right now; the window source only looks while it is.</summary>
public interface IGameSession
{
    bool IsRunning { get; }

    event EventHandler<bool>? RunningChanged;
}

/// <summary>
///     Why a frame was kept for review, as a closed list: the words a player reads for each live in the app's
///     copy (D36), and the free-form detail stays in the note beside the frame and in the log. The first seven
///     are screens that could not be read — four result screens, three song lists (D47, D49); the rest are
///     plays that were read and that PIU Scores did not record. Notes store the name, never the number.
/// </summary>
public enum KeptBecause
{
    NumbersUnreadable,
    NumbersNotShown,
    NumbersDisagree,
    TitleUnreadable,
    ListUnreadable,
    GradeDisagrees,
    TitleUnmatched,
    Refused,
    SongUnknown,
    TokenRejected,
    NotConnected,
    RateLimited,
    Unreachable
}

public static class Kept
{
    /// <summary>A play that was read and not recorded, as opposed to a screen that could not be read.</summary>
    public static bool WasRead(this KeptBecause because) => because >= KeptBecause.Refused;

    /// <summary>A song list kept during a bulk capture, as opposed to a result screen.</summary>
    public static bool IsSongList(this KeptBecause because) => because is KeptBecause.ListUnreadable or KeptBecause.GradeDisagrees
        or KeptBecause.TitleUnmatched;

    /// <summary>Why a play the site did not record was kept.</summary>
    public static KeptBecause Because(PostOutcome outcome) => outcome switch
    {
        PostOutcome.Refused => KeptBecause.Refused,
        PostOutcome.SongUnknown => KeptBecause.SongUnknown,
        PostOutcome.Unauthorized => KeptBecause.TokenRejected,
        PostOutcome.NotConnected => KeptBecause.NotConnected,
        PostOutcome.RateLimited => KeptBecause.RateLimited,
        PostOutcome.Failed => KeptBecause.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A recorded play is not kept.")
    };
}

/// <summary>Keeps a frame the pipeline could not turn into a recorded play, for review; returns where it went.</summary>
public interface IFailedScreenStore
{
    string Save(CapturedFrame frame, KeptBecause because, string detail, ResultScreenReading? reading);
}

/// <summary>What the player is told about. The App turns each notice into a toast; the copy is the owner's.</summary>
[ExcludeFromCodeCoverage]
public abstract record WatcherNotice
{
    /// <summary>A play the site recorded.</summary>
    public sealed record Recorded(ObservedPlay Play, PostOutcome.Recorded Outcome) : WatcherNotice;

    /// <summary>
    ///     A play that was read and not recorded — refused, the song unknown, the token, rate limited,
    ///     unreachable — kept for review at <paramref name="SavedTo" />. The pipeline reports the fact; what
    ///     the player is shown for it (a token outcome is the token's notification) is the App's call.
    /// </summary>
    public sealed record NotRecorded(ObservedPlay Play, PostOutcome Outcome, string SavedTo) : WatcherNotice;

    /// <summary>The stored token was refused when it was checked at start-up; no play is involved.</summary>
    public sealed record TokenRejected : WatcherNotice;

    /// <summary>The first launch after an update.</summary>
    public sealed record Updated(string Version) : WatcherNotice;

    /// <summary>A bulk capture run ended: what it sent, what was already there, what it could not send (D50).</summary>
    public sealed record BulkCaptureFinished(BulkTally Tally) : WatcherNotice;

    /// <summary>A result screen that could not be read, kept at <paramref name="SavedTo" />.</summary>
    public sealed record Unreadable(string Reason, string SavedTo) : WatcherNotice;
}

public interface INotifier
{
    void Notify(WatcherNotice notice);
}

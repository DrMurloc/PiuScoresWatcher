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

/// <summary>Why a frame was kept for review: the reader could not make a play of it, or the site did not record the play.</summary>
public enum KeptFor
{
    Unreadable,
    NotRecorded
}

/// <summary>Keeps a frame the pipeline could not turn into a recorded play, for review; returns where it went.</summary>
public interface IFailedScreenStore
{
    string Save(CapturedFrame frame, KeptFor why, string reason, ResultScreenReading? reading);
}

/// <summary>What the player is told about. The App turns each notice into a toast; the copy is the owner's.</summary>
[ExcludeFromCodeCoverage]
public abstract record WatcherNotice
{
    /// <summary>A play the site recorded.</summary>
    public sealed record Recorded(ObservedPlay Play, PostOutcome.Recorded Outcome) : WatcherNotice;

    /// <summary>A play the site would not take — refused, the song unknown, rate limited, unreachable — kept for review at <paramref name="SavedTo" />.</summary>
    public sealed record NotRecorded(ObservedPlay Play, PostOutcome Outcome, string SavedTo) : WatcherNotice;

    /// <summary>The token stopped working; nothing posts until it is replaced, and every play meanwhile is kept for review.</summary>
    public sealed record TokenRejected : WatcherNotice;

    /// <summary>The first launch after an update.</summary>
    public sealed record Updated(string Version) : WatcherNotice;

    /// <summary>A result screen that could not be read, kept at <paramref name="SavedTo" />.</summary>
    public sealed record Unreadable(string Reason, string SavedTo) : WatcherNotice;
}

public interface INotifier
{
    void Notify(WatcherNotice notice);
}

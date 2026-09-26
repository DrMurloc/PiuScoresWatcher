namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     One turn at PIU Scores' plays at a time: a frame being handled, which may post a play, or a session being ended
///     (D79). A close waits for the frame before it and a frame for the close before it, so a close never overtakes a
///     post: a play the site records after a close starts a new session, as it should, and never one the close meant to
///     end.
/// </summary>
public sealed class PlayGate
{
    public SemaphoreSlim Turn { get; } = new(1, 1);
}

using System.IO;
using System.Media;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Sounds;

namespace PiuScoresWatcher.App.Sounds;

/// <summary>The watcher's four sounds (D50, D77), synthesized once at start-up and played by Windows.</summary>
public enum CaptureSound
{
    Chime,
    Tick,
    LowTone,

    /// <summary>A Perfect Game on PIU Scores (D77).</summary>
    LevelUp
}

/// <summary>
///     Plays the chime, the tick, the low tone or Level up: for what a bulk capture did with a chart (D50), and for
///     what became of a play (D63, D64), each unless its switch is off; Level up whenever a Perfect Game lands (D77).
///     Playing never blocks: Windows plays in the background, and a new sound cuts the last one short, which is what a
///     quick flip through the list wants.
/// </summary>
public sealed class CaptureSounds(ISettingsStore settings) : IDisposable
{
    private readonly SoundPlayer _chime = Player(Tones.Sent);
    private readonly SoundPlayer _levelUp = Player(Tones.PerfectGame);
    private readonly SoundPlayer _lowTone = Player(Tones.NotSent);
    private readonly SoundPlayer _tick = Player(Tones.Already);

    public void Dispose()
    {
        _chime.Dispose();
        _tick.Dispose();
        _lowTone.Dispose();
        _levelUp.Dispose();
    }

    /// <summary>
    ///     The one sound for what a bulk capture's frame did, when its switch is on: Level up when a Perfect Game went up,
    ///     from a row or as the lit chart's best, else the lit chart's own. The rows are heard only when they send (D76).
    /// </summary>
    public void Play(BulkOutcome outcome)
    {
        if (!settings.Load().BulkCaptureSounds)
            return;
        if (outcome.PerfectGames.Any(perfectGame => perfectGame is PerfectGameOutcome.Sent) || outcome is BulkOutcome.Sent { Play.IsPerfectGame: true })
        {
            Preview(CaptureSound.LevelUp);
            return;
        }

        switch (outcome)
        {
            case BulkOutcome.Sent:
                Preview(CaptureSound.Chime);
                break;
            case BulkOutcome.AlreadyThere:
                Preview(CaptureSound.Tick);
                break;
            case BulkOutcome.Unreadable or BulkOutcome.NotRecorded:
                Preview(CaptureSound.LowTone);
                break;
        }
    }

    /// <summary>
    ///     The sound for what became of a play, when its switch is on (D64): the chime when it is on PIU Scores — Level up
    ///     for a Perfect Game (D77) — the low tone when its screen couldn't be read, the tick when PIU Scores didn't take
    ///     it. True when one played, so the play's notification can keep quiet.
    /// </summary>
    public bool Play(WatcherNotice notice)
    {
        CaptureSound? sound = notice switch
        {
            WatcherNotice.Recorded { Play.IsPerfectGame: true } => CaptureSound.LevelUp,
            WatcherNotice.Recorded => CaptureSound.Chime,
            WatcherNotice.Unreadable => CaptureSound.LowTone,
            WatcherNotice.NotRecorded => CaptureSound.Tick,
            _ => null
        };
        if (sound is not { } play || !settings.Load().PlaySounds)
            return false;
        Preview(play);
        return true;
    }

    /// <summary>A sound on its own, whatever the switches say: the start window's play buttons.</summary>
    public void Preview(CaptureSound sound)
    {
        (sound switch
        {
            CaptureSound.Chime => _chime,
            CaptureSound.Tick => _tick,
            CaptureSound.LevelUp => _levelUp,
            _ => _lowTone
        }).Play();
    }

    private static SoundPlayer Player(IReadOnlyList<Tone> tones)
    {
        var player = new SoundPlayer(new MemoryStream(Tones.Wav(tones)));
        player.Load();
        return player;
    }
}

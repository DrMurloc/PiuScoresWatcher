using System.IO;
using System.Media;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Sounds;

namespace PiuScoresWatcher.App.Sounds;

/// <summary>The watcher's three sounds (D50), synthesized once at start-up and played by Windows.</summary>
public enum CaptureSound
{
    Chime,
    Tick,
    LowTone
}

/// <summary>
///     Plays the chime, the tick or the low tone: for what a bulk capture did with a chart (D50), and for what
///     became of a play (D63, D64), each unless its switch is off. Playing never blocks: Windows plays in the
///     background, and a new sound cuts the last one short, which is what a quick flip through the list wants.
/// </summary>
public sealed class CaptureSounds(ISettingsStore settings) : IDisposable
{
    private readonly SoundPlayer _chime = Player(Tones.Sent);
    private readonly SoundPlayer _lowTone = Player(Tones.NotSent);
    private readonly SoundPlayer _tick = Player(Tones.Already);

    public void Dispose()
    {
        _chime.Dispose();
        _tick.Dispose();
        _lowTone.Dispose();
    }

    /// <summary>The sound for what a bulk capture did with a chart, when its switch is on.</summary>
    public void Play(BulkOutcome outcome)
    {
        if (!settings.Load().BulkCaptureSounds)
            return;
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
    ///     The sound for what became of a play, when its switch is on (D64): the chime when it is on PIU Scores, the
    ///     low tone when its screen couldn't be read, the tick when PIU Scores didn't take it. True when one played,
    ///     so the play's notification can keep quiet.
    /// </summary>
    public bool Play(WatcherNotice notice)
    {
        CaptureSound? sound = notice switch
        {
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

using System.IO;
using System.Media;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Sounds;

namespace PiuScoresWatcher.App.Sounds;

/// <summary>The three sounds a bulk capture plays (D50), synthesized once at start-up and played by Windows.</summary>
public enum CaptureSound
{
    Sent,
    Already,
    NotSent
}

/// <summary>
///     Plays the chime, the tick or the low tone for what a bulk capture did with a chart, unless the player
///     switched the sounds off. Playing never blocks: Windows plays in the background, and a new sound cuts
///     the last one short, which is what a quick flip through the list wants.
/// </summary>
public sealed class CaptureSounds(ISettingsStore settings) : IDisposable
{
    private readonly SoundPlayer _already = Player(Tones.Already);
    private readonly SoundPlayer _notSent = Player(Tones.NotSent);
    private readonly SoundPlayer _sent = Player(Tones.Sent);

    public void Dispose()
    {
        _sent.Dispose();
        _already.Dispose();
        _notSent.Dispose();
    }

    /// <summary>The sound for what a frame did, when the switch is on.</summary>
    public void Play(BulkOutcome outcome)
    {
        if (!settings.Load().BulkCaptureSounds)
            return;
        switch (outcome)
        {
            case BulkOutcome.Sent:
                Preview(CaptureSound.Sent);
                break;
            case BulkOutcome.AlreadyThere:
                Preview(CaptureSound.Already);
                break;
            case BulkOutcome.Unreadable or BulkOutcome.NotRecorded:
                Preview(CaptureSound.NotSent);
                break;
        }
    }

    /// <summary>A sound on its own, whatever the switch says: the start window's play buttons.</summary>
    public void Preview(CaptureSound sound)
    {
        (sound switch
        {
            CaptureSound.Sent => _sent,
            CaptureSound.Already => _already,
            _ => _notSent
        }).Play();
    }

    private static SoundPlayer Player(IReadOnlyList<Tone> tones)
    {
        var player = new SoundPlayer(new MemoryStream(Tones.Wav(tones)));
        player.Load();
        return player;
    }
}

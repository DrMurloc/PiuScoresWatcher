using PiuScoresWatcher.Core.Sounds;

namespace PiuScoresWatcher.Tests.SoundTests;

/// <summary>The bulk capture's three sounds come out as WAV files Windows will play (D50).</summary>
public sealed class TonesTests
{
    public static TheoryData<string> Sounds()
    {
        return new TheoryData<string>(["sent", "already", "notSent"]);
    }

    private static Tone[] Named(string name)
    {
        return name switch
        {
            "sent" => Tones.Sent,
            "already" => Tones.Already,
            _ => Tones.NotSent
        };
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EachSoundIsA16BitMonoPcmWave(string name)
    {
        var wav = Tones.Wav(Named(name));

        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));   // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));   // mono
        Assert.Equal(Tones.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EachSoundIsShortAndNeverClips(string name)
    {
        var wav = Tones.Wav(Named(name));

        var seconds = (wav.Length - 44) / 2.0 / Tones.SampleRate;
        Assert.InRange(seconds, 0.05, 0.5);
        var loudest = 0;
        for (var i = 44; i < wav.Length; i += 2)
            loudest = Math.Max(loudest, Math.Abs((int)BitConverter.ToInt16(wav, i)));
        Assert.InRange(loudest, 1000, short.MaxValue - 1);
    }

    [Fact]
    public void TheThreeSoundsDiffer()
    {
        Assert.NotEqual(Tones.Wav(Tones.Sent), Tones.Wav(Tones.Already));
        Assert.NotEqual(Tones.Wav(Tones.Already), Tones.Wav(Tones.NotSent));
    }
}

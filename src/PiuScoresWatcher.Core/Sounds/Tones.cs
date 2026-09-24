using System.Text;

namespace PiuScoresWatcher.Core.Sounds;

public enum Waveform
{
    Sine,
    Triangle,
    Square,
    Sawtooth,

    /// <summary>White noise from a fixed seed, so a sound is the same bytes every time: a snap or a click.</summary>
    Noise
}

/// <summary>
///     One note: when it starts, how long it rings, its shape, and how loud it is beside the sound's other
///     notes (0–1). It rises in <paramref name="Attack" /> seconds and falls away exponentially to
///     <paramref name="Tail" /> of its peak by the end, where it fades out in a few milliseconds.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record Tone(double Frequency, double Start, double Duration, Waveform Wave, double Peak, double Attack = 0.002, double Tail = 0.0001);

/// <summary>
///     The bulk capture's three sounds (D50), synthesized rather than shipped as files: a snap and a bright
///     two-note ding for sent, a short click for already there, a descending buzz for not sent. The first play
///     test lost the mock's soft beeps under Warm Up's song previews, so every sound starts within a couple of
///     milliseconds, sits where music leaves room — high and bright, or buzzy with overtones — and is mixed as
///     loud as a WAV goes without distorting. The player turns the watcher down in Windows' volume mixer, and
///     the App hands the bytes to Windows.
/// </summary>
public static class Tones
{
    public const int SampleRate = 44_100;

    /// <summary>What every sound peaks at: one decibel short of full scale.</summary>
    public const double Loudness = 0.89;

    private const double Release = 0.004;

    public static readonly Tone[] Sent =
    [
        new(0, 0, 0.012, Waveform.Noise, 0.6, Attack: 0.0005, Tail: 0.001),
        new(1760, 0, 0.11, Waveform.Triangle, 0.55, Tail: 0.03),
        new(2637, 0.075, 0.24, Waveform.Triangle, 0.6, Tail: 0.01)
    ];

    public static readonly Tone[] Already =
    [
        new(0, 0, 0.008, Waveform.Noise, 0.7, Attack: 0.0005, Tail: 0.001),
        new(3136, 0, 0.05, Waveform.Triangle, 0.5, Attack: 0.001, Tail: 0.001)
    ];

    public static readonly Tone[] NotSent =
    [
        new(311.1, 0, 0.13, Waveform.Sawtooth, 0.5, Attack: 0.003, Tail: 0.3),
        new(233.1, 0.17, 0.2, Waveform.Sawtooth, 0.5, Attack: 0.003, Tail: 0.05)
    ];

    /// <summary>A 16-bit mono PCM WAV of the notes mixed and brought up to <see cref="Loudness" />, with a moment of silence after the last.</summary>
    public static byte[] Wav(IReadOnlyList<Tone> tones)
    {
        var seconds = tones.Max(tone => tone.Start + tone.Duration) + 0.03;
        var samples = new double[(int)Math.Ceiling(seconds * SampleRate)];
        foreach (var tone in tones)
            Render(tone, samples);
        var loudest = samples.Max(Math.Abs);
        var gain = loudest > 0 ? Loudness / loudest : 0;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            var dataBytes = samples.Length * 2;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            foreach (var sample in samples)
                writer.Write((short)Math.Round(Math.Clamp(sample * gain, -1, 1) * short.MaxValue));
        }

        return stream.ToArray();
    }

    private static void Render(Tone tone, double[] samples)
    {
        var first = (int)(tone.Start * SampleRate);
        var count = (int)(tone.Duration * SampleRate);
        var noise = 0x9E3779B9u;
        for (var i = 0; i < count && first + i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = t < tone.Attack
                ? tone.Peak * t / tone.Attack
                : tone.Peak * Math.Pow(tone.Tail, (t - tone.Attack) / Math.Max(tone.Duration - tone.Attack, 1e-6));
            var left = tone.Duration - t;
            if (left < Release)
                envelope *= left / Release; // no click where the note stops
            var phase = tone.Frequency * t % 1.0;
            double wave;
            switch (tone.Wave)
            {
                case Waveform.Sine:
                    wave = Math.Sin(2 * Math.PI * phase);
                    break;
                case Waveform.Triangle:
                    wave = 1 - 4 * Math.Abs(phase - 0.5);
                    break;
                case Waveform.Square:
                    wave = phase < 0.5 ? 1.0 : -1.0;
                    break;
                case Waveform.Sawtooth:
                    wave = 2 * phase - 1;
                    break;
                default:
                    // xorshift32: the same snap every time
                    noise ^= noise << 13;
                    noise ^= noise >> 17;
                    noise ^= noise << 5;
                    wave = noise / (double)uint.MaxValue * 2 - 1;
                    break;
            }

            samples[first + i] += envelope * wave;
        }
    }
}

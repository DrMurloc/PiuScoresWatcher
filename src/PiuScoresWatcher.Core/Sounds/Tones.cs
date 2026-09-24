using System.Text;

namespace PiuScoresWatcher.Core.Sounds;

public enum Waveform
{
    Sine,
    Triangle,
    Square
}

/// <summary>One note: when it starts, how long it rings, its shape and how loud it peaks (0–1).</summary>
[ExcludeFromCodeCoverage]
public sealed record Tone(double Frequency, double Start, double Duration, Waveform Wave, double Peak);

/// <summary>
///     The bulk capture's three sounds (D50), synthesized rather than shipped as files: a two-note chime for
///     sent, a soft tick for already there, a low double tone for not sent. Each note rises in 12 ms and falls
///     away exponentially, the shape the mock plays. The App hands the WAV bytes to Windows.
/// </summary>
public static class Tones
{
    public const int SampleRate = 44_100;

    public static readonly Tone[] Sent =
    [
        new(1318.5, 0, 0.12, Waveform.Sine, 0.22), new(1975.5, 0.085, 0.22, Waveform.Sine, 0.2)
    ];

    public static readonly Tone[] Already = [new(1046.5, 0, 0.07, Waveform.Triangle, 0.12)];

    public static readonly Tone[] NotSent =
    [
        new(196, 0, 0.14, Waveform.Square, 0.06), new(174.6, 0.18, 0.17, Waveform.Square, 0.06)
    ];

    /// <summary>A 16-bit mono PCM WAV of the notes mixed, with a few milliseconds of silence after the last.</summary>
    public static byte[] Wav(IReadOnlyList<Tone> tones)
    {
        var seconds = tones.Max(tone => tone.Start + tone.Duration) + 0.03;
        var samples = new double[(int)Math.Ceiling(seconds * SampleRate)];
        foreach (var tone in tones)
            Render(tone, samples);

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
                writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * short.MaxValue));
        }

        return stream.ToArray();
    }

    private static void Render(Tone tone, double[] samples)
    {
        const double attack = 0.012;
        var first = (int)(tone.Start * SampleRate);
        var count = (int)(tone.Duration * SampleRate);
        for (var i = 0; i < count && first + i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = t < attack
                ? tone.Peak * t / attack
                : tone.Peak * Math.Pow(0.0001 / tone.Peak, (t - attack) / Math.Max(tone.Duration - attack, 1e-6));
            var phase = tone.Frequency * t % 1.0;
            var wave = tone.Wave switch
            {
                Waveform.Sine => Math.Sin(2 * Math.PI * phase),
                Waveform.Triangle => 1 - 4 * Math.Abs(phase - 0.5),
                _ => phase < 0.5 ? 1.0 : -1.0
            };
            samples[first + i] += envelope * wave;
        }
    }
}

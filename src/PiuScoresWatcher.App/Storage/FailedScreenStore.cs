using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     A frame the pipeline could not turn into a play goes under <c>failed\</c> as a PNG beside a
///     JSON note saying why and what was read. Nothing here leaves the machine until the player
///     chooses to send it.
/// </summary>
public sealed class FailedScreenStore(IClock clock) : IFailedScreenStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Save(CapturedFrame frame, string reason, ResultScreenReading? reading)
    {
        Directory.CreateDirectory(AppPaths.Failed);
        var stem = $"{clock.Now:yyyyMMdd-HHmmss}-{frame.Source.Token()}";
        var path = Path.Combine(AppPaths.Failed, stem + ".png");
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(AppPaths.Failed, $"{stem}-{n}.png");

        var bitmap = BitmapSource.Create(frame.Image.Width, frame.Image.Height, 96, 96, PixelFormats.Bgra32, null,
            Pixels(frame.Image), frame.Image.Width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path))
        {
            encoder.Save(file);
        }

        File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new
        {
            reason,
            source = frame.Source,
            origin = frame.Origin,
            seenAt = frame.SeenAt,
            reading = reading is null
                ? null
                : new
                {
                    reading.Status, reading.Reason, reading.Layout, reading.Mix, reading.ChartType, reading.Level,
                    judgments = reading.Judgments?.ToString(), reading.MaxCombo, reading.Score, reading.AccuracyDigits,
                    reading.IsBroken, reading.LowestGlyphScore
                }
        }, Json));
        return path;
    }

    private static byte[] Pixels(ScreenImage image)
    {
        var bytes = new byte[image.Width * image.Height * 4];
        var i = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            bytes[i++] = image.Blue(x, y);
            bytes[i++] = image.Green(x, y);
            bytes[i++] = image.Red(x, y);
            bytes[i++] = 255;
        }

        return bytes;
    }
}

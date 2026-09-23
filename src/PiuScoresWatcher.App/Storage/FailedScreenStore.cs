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

/// <summary>One frame kept for review, as the review window shows it; <paramref name="Detail" /> is for the log and the developer, never the player.</summary>
public sealed record KeptScreen(string ImagePath, KeptBecause Because, string Detail, CaptureSource Source, DateTimeOffset SeenAt);

/// <summary>
///     A frame the pipeline could not turn into a recorded play goes under <c>failed\</c> as a PNG beside
///     a JSON note saying why and what was read (D31, D38). Nothing here leaves the machine; the player
///     looks at them in the review window and deletes them there.
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

    /// <summary>Something was kept or deleted.</summary>
    public event EventHandler? Changed;

    public string Save(CapturedFrame frame, KeptBecause because, string detail, ResultScreenReading? reading)
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
            because,
            detail,
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
        Changed?.Invoke(this, EventArgs.Empty);
        return path;
    }

    public int Count()
    {
        return Directory.Exists(AppPaths.Failed) ? Directory.EnumerateFiles(AppPaths.Failed, "*.png").Count() : 0;
    }

    /// <summary>What is kept, newest first; a note that cannot be read still lists its image.</summary>
    public IReadOnlyList<KeptScreen> List()
    {
        if (!Directory.Exists(AppPaths.Failed))
            return [];
        return Directory.EnumerateFiles(AppPaths.Failed, "*.png").Select(Describe).OrderByDescending(k => k.SeenAt).ToList();
    }

    public void Delete(KeptScreen screen)
    {
        try
        {
            File.Delete(screen.ImagePath);
            File.Delete(Path.ChangeExtension(screen.ImagePath, ".json"));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // open in another program; it stays listed until a later try succeeds
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static KeptScreen Describe(string image)
    {
        var fallback = new KeptScreen(image, KeptBecause.NumbersUnreadable, "", CaptureSource.GameWindow, File.GetLastWriteTime(image));
        try
        {
            using var note = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(image, ".json")));
            var root = note.RootElement;
            return fallback with
            {
                Because = root.TryGetProperty("because", out var because) && Enum.TryParse<KeptBecause>(because.GetString(), out var kept) ? kept : fallback.Because,
                Detail = root.TryGetProperty("detail", out var detail) ? detail.GetString() ?? "" : "",
                Source = root.TryGetProperty("source", out var source) && Enum.TryParse<CaptureSource>(source.GetString(), out var from) ? from : CaptureSource.GameWindow,
                SeenAt = root.TryGetProperty("seenAt", out var seen) && seen.TryGetDateTimeOffset(out var at) ? at : fallback.SeenAt
            };
        }
        catch (Exception failure) when (failure is IOException or JsonException)
        {
            return fallback;
        }
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

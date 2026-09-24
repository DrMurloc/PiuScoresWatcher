using System.Text.Json;
using PiuScoresWatcher.Core.Recognition;
using SkiaSharp;

namespace PiuScoresWatcher.Tests.TestHelpers;

/// <summary>
///     What one fixture screen is expected to read as. <c>Kind</c> is result, unsettled, empty, aggregate or
///     none for the result screens; songlist, songlist-empty or arcadelist for the song lists (D45).
/// </summary>
internal sealed record ExpectedScreen(
    string Kind, string? Layout, string? Mix, string? Title, string? ChartType, int? Level,
    int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses, int? MaxCombo, int? Score, string? Accuracy, bool? Broken,
    string? Grade = null);

/// <summary>The owner's RISE screens under Fixtures/screens, player card blacked out, with their expected readings.</summary>
internal static class FixtureScreens
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "screens");

    public static IReadOnlyDictionary<string, ExpectedScreen> Expected { get; } =
        JsonSerializer.Deserialize<Dictionary<string, ExpectedScreen>>(
            File.ReadAllText(Path.Combine(Directory, "expected.json")), Options)!;

    public static IEnumerable<string> OfKind(string kind)
    {
        return Expected.Where(pair => pair.Value.Kind == kind).Select(pair => pair.Key).OrderBy(name => name, StringComparer.Ordinal);
    }

    /// <summary>Decodes a fixture into the BGRA frame the reader takes — the one thing Core leaves to an adapter.</summary>
    public static ScreenImage Load(string name)
    {
        using var decoded = SKBitmap.Decode(Path.Combine(Directory, name + ".jpg"))
                            ?? throw new InvalidOperationException($"Fixture {name} did not decode.");
        using var bgra = decoded.ColorType == SKColorType.Bgra8888 ? decoded.Copy() : decoded.Copy(SKColorType.Bgra8888);
        return new ScreenImage(bgra.Width, bgra.Height, bgra.Bytes);
    }

    /// <summary>A fixture with one region's pixels taken from another fixture, in the same place.</summary>
    public static ScreenImage LoadWithRegionOf(string name, string other, FractionRect region)
    {
        var image = Load(name);
        var donor = Load(other);
        var rect = region.On(image);
        var width = image.Width;
        var pasted = new byte[width * image.Height * 4];
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < width; x++)
        {
            var from = rect.X0 <= x && x < rect.X1 && rect.Y0 <= y && y < rect.Y1 ? donor : image;
            var at = (y * width + x) * 4;
            (pasted[at], pasted[at + 1], pasted[at + 2], pasted[at + 3]) = (from.Blue(x, y), from.Green(x, y), from.Red(x, y), 255);
        }

        return new ScreenImage(width, image.Height, pasted);
    }

    /// <summary>A fixture with one region's pixels pasted over another's — a misread made to order.</summary>
    public static ScreenImage LoadWithCopy(string name, FractionRect from, FractionRect to)
    {
        using var decoded = SKBitmap.Decode(Path.Combine(Directory, name + ".jpg"))
                            ?? throw new InvalidOperationException($"Fixture {name} did not decode.");
        using var bgra = decoded.ColorType == SKColorType.Bgra8888 ? decoded.Copy() : decoded.Copy(SKColorType.Bgra8888);
        var original = bgra.Bytes;
        var sized = new ScreenImage(bgra.Width, bgra.Height, original);
        var (source, target) = (from.On(sized), to.On(sized));
        var pasted = (byte[])original.Clone();
        for (var y = 0; y < Math.Min(source.Height, target.Height); y++)
            Array.Copy(original, ((source.Y0 + y) * bgra.Width + source.X0) * 4,
                pasted, ((target.Y0 + y) * bgra.Width + target.X0) * 4, Math.Min(source.Width, target.Width) * 4);
        return new ScreenImage(bgra.Width, bgra.Height, pasted);
    }
}

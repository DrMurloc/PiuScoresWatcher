using System.Text.Json;
using PiuScoresWatcher.Core.Recognition;
using SkiaSharp;

namespace PiuScoresWatcher.Tests.TestHelpers;

/// <summary>What one fixture screen is expected to read as; <c>Kind</c> is result, empty, aggregate or none.</summary>
internal sealed record ExpectedScreen(
    string Kind, string? Layout, string? Mix, string? Title, string? ChartType, int? Level,
    int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses, int? MaxCombo, int? Score, string? Accuracy, bool? Broken);

/// <summary>The owner's result screens under Fixtures/screens, player card blacked out, with their expected readings.</summary>
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
}

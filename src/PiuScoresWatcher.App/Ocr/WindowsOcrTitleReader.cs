using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Recognition;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PiuScoresWatcher.App.Ocr;

/// <summary>
///     Reads the song title with the OCR built into Windows, page by page as <see cref="TitleInk.Pages" />
///     lays them out — dark letters on white with paper around them, at the 1080p size, then larger, then with
///     the gaps closed — handing on each new reading until the caller has the one it wants (D53, D55).
/// </summary>
public sealed class WindowsOcrTitleReader(ILogger<WindowsOcrTitleReader> log) : ITitleReader
{
    public async IAsyncEnumerable<string> ReadAsync(ScreenImage image, PixelRect region, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            log.LogWarning("Windows OCR has no language pack to read with; the title stays unread");
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in TitleInk.Pages(image, region))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await ReadPageAsync(engine, page);
            if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                yield return text;
        }
    }

    private static async Task<string?> ReadPageAsync(OcrEngine engine, GrayImage page)
    {
        var pixels = new byte[page.Width * page.Height * 4];
        for (var i = 0; i < page.Pixels.Length; i++)
        {
            var shade = page.Pixels[i];
            pixels[i * 4] = shade;
            pixels[i * 4 + 1] = shade;
            pixels[i * 4 + 2] = shade;
            pixels[i * 4 + 3] = 255;
        }

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, page.Width, page.Height, BitmapAlphaMode.Ignore);
        var result = await engine.RecognizeAsync(bitmap);
        return result.Text?.Trim();
    }
}

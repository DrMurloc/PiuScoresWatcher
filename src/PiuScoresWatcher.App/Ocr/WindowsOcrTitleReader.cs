using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Recognition;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PiuScoresWatcher.App.Ocr;

/// <summary>
///     Reads the song title with the OCR built into Windows, from the region <see cref="TitleInk" /> has
///     turned into dark letters on white at the size they have on a 1080p screen — the shape and size
///     Windows OCR reads best.
/// </summary>
public sealed class WindowsOcrTitleReader(ILogger<WindowsOcrTitleReader> log) : ITitleReader
{
    public async Task<string?> ReadAsync(ScreenImage image, PixelRect region, CancellationToken cancellationToken)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            log.LogWarning("Windows OCR has no language pack to read with; the title stays unread");
            return null;
        }

        var page = TitleInk.Render(image, region);
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
        cancellationToken.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(bitmap);
        var text = result.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

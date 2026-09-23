using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Recognition;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PiuScoresWatcher.App.Ocr;

/// <summary>
///     Reads the song title with the OCR built into Windows. The title is white text on the yellow
///     bar (or the Arcade Station's blue band), so the region is turned into black text on white
///     and doubled in size first — the shape OCR was made for.
/// </summary>
public sealed class WindowsOcrTitleReader(ILogger<WindowsOcrTitleReader> log) : ITitleReader
{
    private const int Scale = 2;
    private const int BrightText = 200;

    public async Task<string?> ReadAsync(ScreenImage image, PixelRect region, CancellationToken cancellationToken)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            log.LogWarning("Windows OCR has no language pack to read with; the title stays unread");
            return null;
        }

        var width = region.Width * Scale;
        var height = region.Height * Scale;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var sx = region.X0 + x / Scale;
            var sy = region.Y0 + y / Scale;
            var luminance = 0.299 * image.Red(sx, sy) + 0.587 * image.Green(sx, sy) + 0.114 * image.Blue(sx, sy);
            var shade = (byte)(luminance > BrightText ? 0 : 255);
            var offset = (y * width + x) * 4;
            pixels[offset] = shade;
            pixels[offset + 1] = shade;
            pixels[offset + 2] = shade;
            pixels[offset + 3] = 255;
        }

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(bitmap);
        var text = result.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

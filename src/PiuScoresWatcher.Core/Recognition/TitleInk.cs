namespace PiuScoresWatcher.Core.Recognition;

/// <summary>A picture in 8-bit grey, row by row: 255 is white paper, 0 is full ink.</summary>
public sealed record GrayImage(int Width, int Height, byte[] Pixels);

/// <summary>
///     Turns a song title's region into what Windows OCR reads best: dark letters on white paper, at the
///     size the title has on a 1080p screen. A title is white wherever it sits — on the yellow bar, on the
///     Arcade Station's blue band, over a song's jacket on Warm Up's list — so a pixel's ink is how bright
///     it is times how colourless, softly, which keeps the letters' anti-aliased edges and leaves bright
///     colours in the art out. The cut this replaced ("brighter than 200", at double size) let those
///     colours through as noise, and the doubled size made Windows OCR drop short titles outright: over
///     the fixtures it read 13 of 38 titles exactly, this way 33 (D53; <c>tools/reader-lab/titles.py</c>).
/// </summary>
public static class TitleInk
{
    /// <summary>The screen height the output is sized for; any other frame is scaled to it.</summary>
    public const int ReferenceHeight = 1080;

    public static GrayImage Render(ScreenImage image, PixelRect region)
    {
        var sourceWidth = region.Width;
        var sourceHeight = region.Height;
        var ink = new double[sourceWidth * sourceHeight];
        for (var y = 0; y < sourceHeight; y++)
        for (var x = 0; x < sourceWidth; x++)
            ink[y * sourceWidth + x] = Ink(image.Red(region.X0 + x, region.Y0 + y), image.Green(region.X0 + x, region.Y0 + y),
                image.Blue(region.X0 + x, region.Y0 + y));

        var scale = (double)ReferenceHeight / image.Height;
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var value = Sample(ink, sourceWidth, sourceHeight, (x + 0.5) / scale - 0.5, (y + 0.5) / scale - 0.5);
            pixels[y * width + x] = (byte)Math.Round(255 * (1 - value));
        }

        return new GrayImage(width, height, pixels);
    }

    /// <summary>How much of a pixel is title: white and colourless is all of it; a colour, a grey or a shadow is none.</summary>
    public static double Ink(byte red, byte green, byte blue)
    {
        var luminance = 0.299 * red + 0.587 * green + 0.114 * blue;
        var spread = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        return Math.Clamp((luminance - 150) / 70, 0, 1) * Math.Clamp((90 - spread) / 50.0, 0, 1);
    }

    /// <summary>Bilinear, clamped at the edges.</summary>
    private static double Sample(double[] values, int width, int height, double x, double y)
    {
        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, width - 1);
        var y1 = Math.Min(y0 + 1, height - 1);
        var fx = x - x0;
        var fy = y - y0;
        var top = values[y0 * width + x0] * (1 - fx) + values[y0 * width + x1] * fx;
        var bottom = values[y1 * width + x0] * (1 - fx) + values[y1 * width + x1] * fx;
        return top * (1 - fy) + bottom * fy;
    }
}

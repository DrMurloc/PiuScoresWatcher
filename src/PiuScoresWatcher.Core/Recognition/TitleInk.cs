namespace PiuScoresWatcher.Core.Recognition;

/// <summary>A picture in 8-bit grey, row by row: 255 is white paper, 0 is full ink.</summary>
public sealed record GrayImage(int Width, int Height, byte[] Pixels);

/// <summary>
///     Turns a song title's region into what Windows OCR reads best: dark letters on white paper, at the
///     size the title has on a 1080p screen, with a margin of paper around them. A title is white wherever it
///     sits — on the yellow bar, on the Arcade Station's blue band, over a song's jacket on Warm Up's list — so a
///     pixel's ink is how bright it is times how colourless, softly, which keeps the letters' anti-aliased edges
///     and leaves bright colours in the art out (D53). The margin is what short titles needed: without it
///     Windows OCR returned nothing for VANISH, Cynical or Aragami even on a clean page (D55).
///     <para>
///         <see cref="Pages" /> is the order the title is tried in, and the reader stops at the first page whose
///         reading names a chart: the page at 1080p, then at twice the size, then with the wide gaps between
///         letters closed up — "8 6" is two lone characters to Windows until it is one word — and last with the
///         strokes thinned as well, which is what Warm Up's song list, in its heavy outlined type, needed.
///     </para>
/// </summary>
public static class TitleInk
{
    /// <summary>The screen height the output is sized for; any other frame is scaled to it.</summary>
    public const int ReferenceHeight = 1080;

    /// <summary>The paper around the letters, at the 1080p size.</summary>
    public const int Margin = 40;

    /// <summary>Anything darker than this in a column is ink when gaps are measured.</summary>
    private const byte InkShade = 160;

    /// <summary>The pages to try, in order.</summary>
    public static IEnumerable<GrayImage> Pages(ScreenImage image, PixelRect region)
    {
        var page = Render(image, region);
        yield return Pad(page, Margin);
        yield return Pad(Render(image, region, 2), Margin * 2);
        var closed = CloseGaps(page);
        yield return Pad(closed, Margin);
        yield return Pad(Thin(closed), Margin);
    }

    /// <summary>The region as dark letters on white, at <paramref name="size" /> times the size it has on a 1080p screen.</summary>
    public static GrayImage Render(ScreenImage image, PixelRect region, double size = 1)
    {
        var sourceWidth = region.Width;
        var sourceHeight = region.Height;
        var ink = new double[sourceWidth * sourceHeight];
        for (var y = 0; y < sourceHeight; y++)
        for (var x = 0; x < sourceWidth; x++)
            ink[y * sourceWidth + x] = Ink(image.Red(region.X0 + x, region.Y0 + y), image.Green(region.X0 + x, region.Y0 + y),
                image.Blue(region.X0 + x, region.Y0 + y));

        var scale = ReferenceHeight * size / image.Height;
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

    /// <summary>The page with <paramref name="margin" /> pixels of paper on every side.</summary>
    public static GrayImage Pad(GrayImage page, int margin)
    {
        var width = page.Width + 2 * margin;
        var height = page.Height + 2 * margin;
        var pixels = new byte[width * height];
        Array.Fill(pixels, (byte)255);
        for (var y = 0; y < page.Height; y++)
            Array.Copy(page.Pixels, y * page.Width, pixels, (y + margin) * width + margin, page.Width);
        return new GrayImage(width, height, pixels);
    }

    /// <summary>
    ///     The page with every stretch of paper before a letter wider than 0.4 of the line's height cut to 0.15
    ///     of it — the gaps inside the title, and the paper ahead of it; what follows the last letter stays.
    /// </summary>
    public static GrayImage CloseGaps(GrayImage page)
    {
        var inkColumns = new bool[page.Width];
        for (var x = 0; x < page.Width; x++)
        for (var y = 0; y < page.Height && !inkColumns[x]; y++)
            inkColumns[x] = page.Pixels[y * page.Width + x] < InkShade;

        var wide = page.Height * 0.4;
        var kept = Math.Max(1, (int)(page.Height * 0.15));
        var columns = new List<int>(page.Width);
        var gap = new List<int>();
        for (var x = 0; x < page.Width; x++)
        {
            if (!inkColumns[x])
            {
                gap.Add(x);
                continue;
            }

            columns.AddRange(gap.Count > wide ? gap.Take(kept) : gap);
            gap.Clear();
            columns.Add(x);
        }

        columns.AddRange(gap);
        var pixels = new byte[columns.Count * page.Height];
        for (var y = 0; y < page.Height; y++)
        for (var i = 0; i < columns.Count; i++)
            pixels[y * columns.Count + i] = page.Pixels[y * page.Width + columns[i]];
        return new GrayImage(columns.Count, page.Height, pixels);
    }

    /// <summary>The page with every stroke a pixel thinner on each side: each pixel takes the lightest shade around it.</summary>
    public static GrayImage Thin(GrayImage page)
    {
        var pixels = new byte[page.Pixels.Length];
        for (var y = 0; y < page.Height; y++)
        for (var x = 0; x < page.Width; x++)
        {
            byte lightest = 0;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = Math.Clamp(x + dx, 0, page.Width - 1);
                var ny = Math.Clamp(y + dy, 0, page.Height - 1);
                lightest = Math.Max(lightest, page.Pixels[ny * page.Width + nx]);
            }

            pixels[y * page.Width + x] = lightest;
        }

        return new GrayImage(page.Width, page.Height, pixels);
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

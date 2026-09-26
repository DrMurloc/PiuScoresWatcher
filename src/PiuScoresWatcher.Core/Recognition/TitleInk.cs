namespace PiuScoresWatcher.Core.Recognition;

/// <summary>A picture in 8-bit grey, row by row: 255 is white paper, 0 is full ink.</summary>
public sealed record GrayImage(int Width, int Height, byte[] Pixels);

/// <summary>One page for the OCR, and how many copies of the title it prints (D67).</summary>
public sealed record TitlePage(GrayImage Image, int Repeats)
{
    /// <summary>
    ///     What the OCR's reading of the page says the title is: the reading itself, or, on a page of copies, the one
    ///     word they all read as — null when the copies disagree, which is a misread and not a title.
    /// </summary>
    public string? Reading(string text)
    {
        if (Repeats == 1)
            return text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length % Repeats != 0)
            return null;
        var size = words.Length / Repeats;
        var copies = Enumerable.Range(0, Repeats)
            .Select(copy => string.Join(' ', words.Skip(copy * size).Take(size)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return copies.Count == 1 ? copies[0] : null;
    }
}

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
///         letters closed up — "8 6" is two lone characters to Windows until it is one word — then, for a title in
///         heavy outlined type, with the strokes thinned as well, which is what the song list's panel needed; and
///         last, for a short title, the title three times over, which is the only way Windows reads B2, D or N (D67).
///     </para>
/// </summary>
public static class TitleInk
{
    /// <summary>The screen height the output is sized for; any other frame is scaled to it.</summary>
    public const int ReferenceHeight = 1080;

    /// <summary>The paper around the letters, at the 1080p size.</summary>
    public const int Margin = 40;

    /// <summary>How near a scrolling box's right edge, at the 1080p size, a title's ink reaches when the game cut it off there (D68).</summary>
    public const int EdgeWidth = 10;

    /// <summary>Anything darker than this in a column is ink when gaps are measured.</summary>
    private const byte InkShade = 160;

    /// <summary>A title narrower than this many times its height is short, and tried three times over as well (D67).</summary>
    private const double ShortTitle = 3.0;

    /// <summary>The copies a short title's page prints.</summary>
    private const int Copies = 3;

    /// <summary>A column at the box's edge is title when this share of the box's height is ink.</summary>
    private const double EdgeInk = 0.05;

    /// <summary>The space between a short title's copies, as shares of its height: at the narrower one Windows reads B2, at the wider one D and N.</summary>
    private static readonly double[] CopyGaps = [0.6, 1.2];

    /// <summary>The pages to try, in order.</summary>
    public static IEnumerable<TitlePage> Pages(ScreenImage image, TitleBox box)
    {
        var page = Render(image, box.Region);
        yield return new TitlePage(Pad(page, Margin), 1);
        yield return new TitlePage(Pad(Render(image, box.Region, 2), Margin * 2), 1);
        var closed = CloseGaps(page);
        yield return new TitlePage(Pad(closed, Margin), 1);
        if (box.Heavy)
            yield return new TitlePage(Pad(Thin(closed), Margin), 1);
        if (Trim(page) is not { } word || word.Width >= ShortTitle * word.Height)
            yield break;
        foreach (var gap in CopyGaps)
            yield return new TitlePage(Pad(Repeat(word, Copies, gap), Margin), Copies);
    }

    /// <summary>
    ///     Whether the title reaches the right edge of its box, where the game cuts off a title too long to fit: what shows
    ///     is then the start of a longer title, never the whole of one (D68).
    /// </summary>
    public static bool RunsOffRight(ScreenImage image, PixelRect region)
    {
        var edge = Math.Max(1, (int)Math.Round(EdgeWidth * image.Height / (double)ReferenceHeight));
        for (var x = Math.Max(region.X0, region.X1 - edge); x < region.X1; x++)
        {
            var ink = 0.0;
            for (var y = region.Y0; y < region.Y1; y++)
                ink += Ink(image.Red(x, y), image.Green(x, y), image.Blue(x, y));
            if (ink >= EdgeInk * region.Height)
                return true;
        }

        return false;
    }

    /// <summary>The page cut down to its ink with a little paper around it; null when there is no ink on it.</summary>
    public static GrayImage? Trim(GrayImage page)
    {
        int left = page.Width, right = -1, top = page.Height, bottom = -1;
        for (var y = 0; y < page.Height; y++)
        for (var x = 0; x < page.Width; x++)
            if (page.Pixels[y * page.Width + x] < InkShade)
                (left, right, top, bottom) = (Math.Min(left, x), Math.Max(right, x), Math.Min(top, y), Math.Max(bottom, y));
        if (right < 0)
            return null;

        const int paper = 4;
        var (x0, y0) = (Math.Max(0, left - paper), Math.Max(0, top - paper));
        var (x1, y1) = (Math.Min(page.Width, right + 1 + paper), Math.Min(page.Height, bottom + 1 + paper));
        var pixels = new byte[(x1 - x0) * (y1 - y0)];
        for (var y = y0; y < y1; y++)
            Array.Copy(page.Pixels, y * page.Width + x0, pixels, (y - y0) * (x1 - x0), x1 - x0);
        return new GrayImage(x1 - x0, y1 - y0, pixels);
    }

    /// <summary><paramref name="times" /> copies of the word side by side, <paramref name="gap" /> of its height apart.</summary>
    public static GrayImage Repeat(GrayImage word, int times, double gap)
    {
        var space = Math.Max(1, (int)(word.Height * gap));
        var width = word.Width * times + space * (times - 1);
        var pixels = new byte[width * word.Height];
        Array.Fill(pixels, (byte)255);
        for (var copy = 0; copy < times; copy++)
        for (var y = 0; y < word.Height; y++)
            Array.Copy(word.Pixels, y * word.Width, pixels, y * width + copy * (word.Width + space), word.Width);
        return new GrayImage(width, word.Height, pixels);
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

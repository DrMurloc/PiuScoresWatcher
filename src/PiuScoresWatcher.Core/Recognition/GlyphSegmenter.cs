namespace PiuScoresWatcher.Core.Recognition;

/// <summary>One glyph's box inside a field mask; <c>X1</c>/<c>Y1</c> exclusive.</summary>
internal readonly record struct Glyph(int X0, int X1, int Y0, int Y1)
{
    public int Width => X1 - X0;
    public int Height => Y1 - Y0;
    public double Aspect => Width / (double)Math.Max(Height, 1);
}

/// <summary>
///     Cuts a field's ink into glyphs by column projection: a run of inked columns is a glyph, a
///     stray line from a neighbouring element is trimmed off its rows, speckle is dropped, and a run
///     much wider than its neighbours is two touching glyphs cut at its thinnest column.
/// </summary>
internal static class GlyphSegmenter
{
    public static IReadOnlyList<Glyph> Segment(Mask mask)
    {
        var glyphs = new List<Glyph>();
        var x = 0;
        while (x < mask.Width)
        {
            if (!mask.ColumnHasInk(x))
            {
                x++;
                continue;
            }

            var start = x;
            while (x < mask.Width && mask.ColumnHasInk(x))
                x++;
            var (y0, y1) = RowBounds(mask, start, x);
            glyphs.Add(new Glyph(start, x, y0, y1));
        }

        glyphs.RemoveAll(g => g.Height <= 3 && g.Width <= 5);
        if (glyphs.Count == 0)
            return glyphs;

        var tallest = glyphs.Max(g => g.Height);
        var kept = glyphs.Where(g => g.Height >= 0.45 * tallest).ToList();
        return SplitWide(mask, kept);
    }

    /// <summary>The rows a glyph really occupies: rows with under a tenth of the peak ink (and under two pixels) are a neighbour's stray line.</summary>
    private static (int Y0, int Y1) RowBounds(Mask mask, int x0, int x1)
    {
        var counts = mask.RowCounts(x0, x1);
        var peak = counts.Max();
        var floor = Math.Max(2, 0.1 * peak);
        var first = -1;
        var last = -1;
        for (var y = 0; y < counts.Length; y++)
        {
            if (counts[y] < floor)
                continue;
            if (first < 0)
                first = y;
            last = y;
        }

        return first < 0 ? (0, 0) : (first, last + 1);
    }

    private static List<Glyph> SplitWide(Mask mask, List<Glyph> glyphs)
    {
        if (glyphs.Count < 2)
            return glyphs;
        var widths = glyphs.Select(g => g.Width).OrderBy(w => w).ToArray();
        var median = widths[widths.Length / 2];
        var result = new List<Glyph>(glyphs.Count + 1);
        foreach (var g in glyphs)
        {
            if (g.Width <= 1.6 * median || g.Width <= 8)
            {
                result.Add(g);
                continue;
            }

            var projection = mask.ColumnCounts(g.X0, g.X1, g.Y0, g.Y1);
            var lo = (int)(projection.Length * 0.3);
            var hi = (int)(projection.Length * 0.7);
            var cut = lo;
            for (var i = lo; i < hi; i++)
                if (projection[i] < projection[cut])
                    cut = i;
            foreach (var (a, b) in new[] { (g.X0, g.X0 + cut), (g.X0 + cut, g.X1) })
            {
                var (y0, y1) = RowBounds(mask, a, b);
                if (y1 > y0)
                    result.Add(new Glyph(a, b, y0, y1));
            }
        }

        return result;
    }
}

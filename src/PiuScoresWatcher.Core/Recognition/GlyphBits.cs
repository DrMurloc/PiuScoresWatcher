using System.Numerics;

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     A glyph reduced to a 12x20 bitmap — the box it occupies, area-averaged onto the grid and
///     thresholded at half — so glyphs of any size in the same font compare by bit agreement.
/// </summary>
internal static class GlyphBits
{
    public const int Width = 12;
    public const int Height = 20;
    public const int Count = Width * Height;
    private const int Words = 4;

    public static ulong[] Of(Mask mask, Glyph glyph)
    {
        var bits = new ulong[Words];
        for (var ty = 0; ty < Height; ty++)
        {
            var sy0 = glyph.Y0 + ty * glyph.Height / (double)Height;
            var sy1 = glyph.Y0 + (ty + 1) * glyph.Height / (double)Height;
            for (var tx = 0; tx < Width; tx++)
            {
                var sx0 = glyph.X0 + tx * glyph.Width / (double)Width;
                var sx1 = glyph.X0 + (tx + 1) * glyph.Width / (double)Width;
                if (Coverage(mask, sx0, sx1, sy0, sy1) >= 0.5)
                    Set(bits, ty * Width + tx);
            }
        }

        return bits;
    }

    /// <summary>Parses the row-major "0101…" form the template file stores.</summary>
    public static ulong[] Parse(string rowMajorBits)
    {
        if (rowMajorBits.Length != Count)
            throw new ArgumentException($"A glyph template has {Count} bits, not {rowMajorBits.Length}.", nameof(rowMajorBits));
        var bits = new ulong[Words];
        for (var i = 0; i < Count; i++)
            if (rowMajorBits[i] == '1')
                Set(bits, i);
        return bits;
    }

    /// <summary>The share of the grid on which two glyphs agree.</summary>
    public static double Similarity(ulong[] a, ulong[] b)
    {
        var differing = 0;
        for (var i = 0; i < Words; i++)
            differing += BitOperations.PopCount(a[i] ^ b[i]);
        return 1.0 - differing / (double)Count;
    }

    private static void Set(ulong[] bits, int index)
    {
        bits[index / 64] |= 1UL << (index % 64);
    }

    /// <summary>The inked share of a fractional source box, each source pixel weighted by how much of it the box covers.</summary>
    private static double Coverage(Mask mask, double x0, double x1, double y0, double y1)
    {
        double inked = 0;
        double total = 0;
        for (var cy = (int)Math.Floor(y0); cy < Math.Ceiling(y1); cy++)
        {
            var wy = Math.Min(y1, cy + 1) - Math.Max(y0, cy);
            if (wy <= 0)
                continue;
            for (var cx = (int)Math.Floor(x0); cx < Math.Ceiling(x1); cx++)
            {
                var wx = Math.Min(x1, cx + 1) - Math.Max(x0, cx);
                if (wx <= 0)
                    continue;
                var w = wx * wy;
                total += w;
                if (cx < mask.Width && cy < mask.Height && mask[cx, cy])
                    inked += w;
            }
        }

        return total <= 0 ? 0 : inked / total;
    }
}

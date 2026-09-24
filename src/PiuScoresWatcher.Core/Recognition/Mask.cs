namespace PiuScoresWatcher.Core.Recognition;

/// <summary>Which pixels of a field are ink — a digit's body — and which are background.</summary>
internal sealed class Mask
{
    private readonly bool[] _cells;

    public Mask(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new bool[Math.Max(0, width * height)];
    }

    public int Width { get; }
    public int Height { get; }

    public bool this[int x, int y]
    {
        get => _cells[y * Width + x];
        set => _cells[y * Width + x] = value;
    }

    public bool ColumnHasInk(int x)
    {
        for (var y = 0; y < Height; y++)
            if (this[x, y])
                return true;
        return false;
    }

    /// <summary>Ink per row, counted over the columns [x0, x1).</summary>
    public int[] RowCounts(int x0, int x1)
    {
        var counts = new int[Height];
        for (var y = 0; y < Height; y++)
        for (var x = x0; x < x1; x++)
            if (this[x, y])
                counts[y]++;
        return counts;
    }

    /// <summary>Ink per column, counted over the rows [y0, y1).</summary>
    public int[] ColumnCounts(int x0, int x1, int y0, int y1)
    {
        var counts = new int[x1 - x0];
        for (var x = x0; x < x1; x++)
        for (var y = y0; y < y1; y++)
            if (this[x, y])
                counts[x - x0]++;
        return counts;
    }
}

/// <summary>The two kinds of ink a result screen prints numbers in.</summary>
internal enum MaskKind
{
    /// <summary>White digits: judgments, max combo, accuracy, the level, the Arcade Station's score. Grey leading zeros stay out.</summary>
    White,

    /// <summary>The gold score digits on the DANCE GRADE screen's red brush stroke.</summary>
    Gold,

    /// <summary>
    ///     The Warm Up song list's digits: colourless and light relative to the field's brightest such
    ///     pixel — white, or grey while the panel fades in, and the grey of an unlit level box — never
    ///     the yellow of the lit one. A fixed bar low enough for the grey lets white digits' edges bridge.
    /// </summary>
    Light
}

internal static class Masks
{
    public static Mask Build(ScreenImage image, PixelRect rect, MaskKind kind)
    {
        var mask = new Mask(rect.Width, rect.Height);
        var lightBar = kind == MaskKind.Light ? LightBar(image, rect) : 0;
        for (var y = rect.Y0; y < rect.Y1; y++)
        for (var x = rect.X0; x < rect.X1; x++)
        {
            var r = image.Red(x, y);
            var g = image.Green(x, y);
            var b = image.Blue(x, y);
            mask[x - rect.X0, y - rect.Y0] = kind switch
            {
                MaskKind.White => IsWhite(r, g, b),
                MaskKind.Gold => IsGold(r, g, b),
                _ => IsColourless(r, g, b) && Colors.Luminance(r, g, b) > lightBar
            };
        }

        return mask;
    }

    /// <summary>Sixty percent of the brightest colourless pixel, never under 100: the panel behind sits near 34.</summary>
    private static double LightBar(ScreenImage image, PixelRect rect)
    {
        var brightest = 0.0;
        for (var y = rect.Y0; y < rect.Y1; y++)
        for (var x = rect.X0; x < rect.X1; x++)
        {
            var r = image.Red(x, y);
            var g = image.Green(x, y);
            var b = image.Blue(x, y);
            if (IsColourless(r, g, b))
                brightest = Math.Max(brightest, Colors.Luminance(r, g, b));
        }

        return Math.Max(100, 0.6 * brightest);
    }

    private static bool IsColourless(byte r, byte g, byte b)
    {
        return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 60;
    }

    /// <summary>Bright and colourless. The grey leading zeros (about 135 luminance) fall well under the bar.</summary>
    private static bool IsWhite(byte r, byte g, byte b)
    {
        var spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        return Colors.Luminance(r, g, b) > 185 && spread < 80;
    }

    /// <summary>Bright and warm. The red brush behind the digits is dark in green, the outline is dark, white sparkles are not warm.</summary>
    private static bool IsGold(byte r, byte g, byte b)
    {
        return Colors.Luminance(r, g, b) > 140 && r - b > 60 && g > 120;
    }
}

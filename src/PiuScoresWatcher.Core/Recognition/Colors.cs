namespace PiuScoresWatcher.Core.Recognition;

/// <summary>A hue band with saturation and brightness floors; the band may wrap through red (0°).</summary>
internal readonly record struct ColorClass(double HueLo, double HueHi, double MinSat, double MinVal)
{
    /// <summary>PERFECT's label.</summary>
    public static readonly ColorClass Blue = new(180, 250, 0.45, 0.55);

    /// <summary>GREAT's label, and the Arcade Station's double stepball.</summary>
    public static readonly ColorClass Green = new(75, 150, 0.45, 0.5);

    /// <summary>GOOD's label.</summary>
    public static readonly ColorClass Yellow = new(35, 65, 0.5, 0.6);

    /// <summary>BAD's label.</summary>
    public static readonly ColorClass Magenta = new(275, 335, 0.4, 0.5);

    /// <summary>MISS's label.</summary>
    public static readonly ColorClass Red = new(345, 15, 0.5, 0.5);

    /// <summary>The Warm Up song list's banner.</summary>
    public static readonly ColorClass BannerYellow = new(35, 60, 0.6, 0.7);

    /// <summary>The lit 5K SINGLE tab's border and caption on the Warm Up song list.</summary>
    public static readonly ColorClass TabOrange = new(15, 40, 0.6, 0.6);

    /// <summary>The lit 6K DOUBLE tab's border and caption on the Warm Up song list (D71).</summary>
    public static readonly ColorClass TabBlue = new(185, 230, 0.6, 0.6);

    /// <summary>The lit level box on the Warm Up song list.</summary>
    public static readonly ColorClass BoxYellow = new(40, 60, 0.6, 0.7);

    public bool Matches(double hue, double sat, double val)
    {
        if (sat < MinSat || val < MinVal)
            return false;
        return HueLo <= HueHi ? hue >= HueLo && hue <= HueHi : hue >= HueLo || hue <= HueHi;
    }
}

/// <summary>What a region of a frame is made of, colour-wise. Every rule the detector uses lives here.</summary>
internal static class Colors
{
    public static double Luminance(byte r, byte g, byte b)
    {
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    /// <summary>Hue in degrees (0–360), saturation and value in 0–1.</summary>
    public static (double Hue, double Sat, double Val) Hsv(byte r, byte g, byte b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;
        var sat = max > 0 ? delta / max : 0;
        if (delta <= 1e-6)
            return (0, sat, max);
        double hue;
        if (max == rf)
            hue = ((gf - bf) / delta + 6) % 6;
        else if (max == gf)
            hue = (bf - rf) / delta + 2;
        else
            hue = (rf - gf) / delta + 4;
        return (hue * 60, sat, max);
    }

    /// <summary>The share of a region's pixels that belong to a colour class.</summary>
    public static double Fraction(ScreenImage image, PixelRect rect, ColorClass cls)
    {
        var total = 0;
        var matched = 0;
        for (var y = rect.Y0; y < rect.Y1; y++)
        for (var x = rect.X0; x < rect.X1; x++)
        {
            total++;
            var (h, s, v) = Hsv(image.Red(x, y), image.Green(x, y), image.Blue(x, y));
            if (cls.Matches(h, s, v))
                matched++;
        }

        return total == 0 ? 0 : matched / (double)total;
    }

    /// <summary>
    ///     Among a region's saturated pixels, the shares that are red, blue and green — how a level
    ///     badge or a stepball says which chart type it is.
    /// </summary>
    public static (double Red, double Blue, double Green) SaturatedHues(ScreenImage image, PixelRect rect)
    {
        var saturated = 0;
        var red = 0;
        var blue = 0;
        var green = 0;
        for (var y = rect.Y0; y < rect.Y1; y++)
        for (var x = rect.X0; x < rect.X1; x++)
        {
            var (h, s, v) = Hsv(image.Red(x, y), image.Green(x, y), image.Blue(x, y));
            if (s <= 0.5 || v <= 0.4)
                continue;
            saturated++;
            if (h < 20 || h > 340)
                red++;
            else if (h > 190 && h < 250)
                blue++;
            else if (h > 80 && h < 160)
                green++;
        }

        return saturated == 0 ? (0, 0, 0) : (red / (double)saturated, blue / (double)saturated, green / (double)saturated);
    }

    /// <summary>Of a region's bright pixels, the share that carries colour — a grey grade sticker has none.</summary>
    public static double SaturatedShareOfBright(ScreenImage image, PixelRect rect)
    {
        var bright = 0;
        var saturated = 0;
        for (var y = rect.Y0; y < rect.Y1; y++)
        for (var x = rect.X0; x < rect.X1; x++)
        {
            var (_, s, v) = Hsv(image.Red(x, y), image.Green(x, y), image.Blue(x, y));
            if (v <= 0.5)
                continue;
            bright++;
            if (s > 0.4)
                saturated++;
        }

        return bright == 0 ? 0 : saturated / (double)bright;
    }

    /// <summary>
    ///     A region's picture in 64 bits: eight by eight cells, one bit each for brighter than the region's average. The
    ///     same picture prints the same bits frame after frame; on the owner's song lists two jackets differ in 18 or more.
    /// </summary>
    public static ulong LuminancePrint(ScreenImage image, PixelRect rect)
    {
        const int cells = 8;
        Span<double> means = stackalloc double[cells * cells];
        for (var cy = 0; cy < cells; cy++)
        for (var cx = 0; cx < cells; cx++)
        {
            var (x0, x1) = (rect.X0 + rect.Width * cx / cells, rect.X0 + rect.Width * (cx + 1) / cells);
            var (y0, y1) = (rect.Y0 + rect.Height * cy / cells, rect.Y0 + rect.Height * (cy + 1) / cells);
            var sum = 0.0;
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                sum += Luminance(image.Red(x, y), image.Green(x, y), image.Blue(x, y));
            var count = (x1 - x0) * (y1 - y0);
            means[cy * cells + cx] = count == 0 ? 0 : sum / count;
        }

        var average = 0.0;
        foreach (var mean in means)
            average += mean;
        average /= means.Length;

        ulong print = 0;
        for (var i = 0; i < means.Length; i++)
            if (means[i] > average)
                print |= 1UL << i;
        return print;
    }
}

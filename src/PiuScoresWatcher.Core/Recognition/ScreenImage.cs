namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     A frame as pixels: BGRA, top-down, four bytes a pixel — the layout a window capture hands
///     over and a decoded screenshot is converted to. Core never decodes a file; an adapter does.
/// </summary>
public sealed class ScreenImage
{
    private readonly byte[] _bgra;

    public ScreenImage(int width, int height, byte[] bgra)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "An image has a positive width and height.");
        if (bgra.Length != (long)width * height * 4)
            throw new ArgumentException($"Expected {(long)width * height * 4} bytes of BGRA for {width}x{height}, got {bgra.Length}.", nameof(bgra));
        Width = width;
        Height = height;
        _bgra = bgra;
    }

    public int Width { get; }
    public int Height { get; }

    public byte Blue(int x, int y) => _bgra[Offset(x, y)];
    public byte Green(int x, int y) => _bgra[Offset(x, y) + 1];
    public byte Red(int x, int y) => _bgra[Offset(x, y) + 2];

    private int Offset(int x, int y) => (y * Width + x) * 4;
}

/// <summary>A rectangle as fractions of the frame, so one layout serves every resolution the game runs at.</summary>
public readonly record struct FractionRect(double X0, double Y0, double X1, double Y1)
{
    /// <summary>Measured on the owner's 1920x1080 screens; the fractions are what the reader keeps.</summary>
    public static FractionRect At1080p(int x0, int y0, int x1, int y1)
    {
        return new FractionRect(x0 / 1920.0, y0 / 1080.0, x1 / 1920.0, y1 / 1080.0);
    }

    public PixelRect On(ScreenImage image)
    {
        return new PixelRect((int)(X0 * image.Width), (int)(Y0 * image.Height), (int)(X1 * image.Width), (int)(Y1 * image.Height));
    }
}

/// <summary>A rectangle in pixels of one frame; <c>X1</c>/<c>Y1</c> are exclusive.</summary>
public readonly record struct PixelRect(int X0, int Y0, int X1, int Y1)
{
    public int Width => X1 - X0;
    public int Height => Y1 - Y0;
}

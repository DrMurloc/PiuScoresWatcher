using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Game-window mode: once a second while RISE runs, the game window's client area is copied
///     into pixels with <c>PrintWindow</c> and handed to the pipeline. A frame that has not changed
///     since the last one is skipped, so a static screen costs a comparison and nothing more. Nothing
///     but the RISE window is ever captured, and the game is never touched.
/// </summary>
public sealed class WindowCaptureSource(RiseProcessWatch game, IClock clock, ILogger<WindowCaptureSource> log) : IScreenSource
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>During a bulk capture: five looks a second, and every frame handed on, still or not (D48).</summary>
    public static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(200);

    private volatile bool _fast;

    public CaptureSource Kind => CaptureSource.GameWindow;

    /// <summary>
    ///     On while a bulk capture runs. The half-second wait needs to see the panel stay the same, so an
    ///     unchanged frame is handed on rather than skipped.
    /// </summary>
    public bool Fast
    {
        get => _fast;
        set => _fast = value;
    }

    public async Task RunAsync(Func<CapturedFrame, Task> onFrame, CancellationToken cancellationToken)
    {
        ulong lastSignature = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var fast = _fast;
            var window = game.MainWindow;
            if (window != 0 && Gdi.IsWindow(window) && !Gdi.IsIconic(window))
            {
                var image = Capture(window);
                if (image is not null)
                {
                    var signature = Signature(image);
                    if (fast || signature != lastSignature)
                    {
                        lastSignature = signature;
                        await onFrame(new CapturedFrame(image, CaptureSource.GameWindow, clock.Now, null));
                    }
                }
            }
            else
            {
                lastSignature = 0;
            }

            try
            {
                await Task.Delay(fast ? FastInterval : Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>The window's client area as BGRA, or null when it has no size or Windows would not render it.</summary>
    private ScreenImage? Capture(nint window)
    {
        if (!Gdi.GetClientRect(window, out var rect))
            return null;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return null;

        var screen = Gdi.GetDC(0);
        var memory = Gdi.CreateCompatibleDC(screen);
        var info = new Gdi.BitmapInfo
        {
            Header = new Gdi.BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<Gdi.BitmapInfoHeader>(),
                Width = width,
                Height = -height, // top-down, the order the reader expects
                Planes = 1,
                BitCount = 32,
                Compression = Gdi.BiRgb
            }
        };
        var bitmap = Gdi.CreateDIBSection(memory, in info, Gdi.DibRgbColors, out var bits, 0, 0);
        try
        {
            if (bitmap == 0)
                return null;
            var previous = Gdi.SelectObject(memory, bitmap);
            var rendered = Gdi.PrintWindow(window, memory, Gdi.PwClientOnly | Gdi.PwRenderFullContent);
            Gdi.SelectObject(memory, previous);
            if (!rendered)
            {
                log.LogDebug("PrintWindow refused the RISE window this tick");
                return null;
            }

            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new ScreenImage(width, height, pixels);
        }
        finally
        {
            if (bitmap != 0)
                Gdi.DeleteObject(bitmap);
            Gdi.DeleteDC(memory);
            Gdi.ReleaseDC(0, screen);
        }
    }

    /// <summary>A cheap fingerprint over a sparse grid of pixels; two frames of the same static screen share it.</summary>
    private static ulong Signature(ScreenImage image)
    {
        ulong hash = 14695981039346656037;
        for (var y = 0; y < image.Height; y += 24)
        for (var x = 0; x < image.Width; x += 24)
        {
            hash = (hash ^ image.Red(x, y)) * 1099511628211;
            hash = (hash ^ image.Green(x, y)) * 1099511628211;
            hash = (hash ^ image.Blue(x, y)) * 1099511628211;
        }

        return hash;
    }
}

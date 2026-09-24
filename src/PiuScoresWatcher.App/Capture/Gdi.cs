using System.Runtime.InteropServices;

namespace PiuScoresWatcher.App.Capture;

/// <summary>The handful of user32/gdi32 calls that copy a window's client area into pixels.</summary>
internal static partial class Gdi
{
    public const uint PwClientOnly = 0x1;

    /// <summary>Render through DWM's composed content, which is what makes a Direct3D window come out as pixels rather than black.</summary>
    public const uint PwRenderFullContent = 0x2;

    public const uint DibRgbColors = 0;
    public const uint BiRgb = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint FirstColor;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PrintWindow(nint window, nint deviceContext, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint window, nint deviceContext);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(nint deviceContext, in BitmapInfo info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint deviceContext, nint gdiObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint gdiObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint deviceContext);
}

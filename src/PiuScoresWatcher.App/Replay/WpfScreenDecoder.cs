using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.App.Replay;

/// <summary>Decodes a screenshot file (JPEG, PNG, anything WPF decodes) into the BGRA frame the reader takes.</summary>
internal static class WpfScreenDecoder
{
    public static ScreenImage Decode(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var stride = frame.PixelWidth * 4;
        var bytes = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(bytes, stride, 0);
        return new ScreenImage(frame.PixelWidth, frame.PixelHeight, bytes);
    }
}

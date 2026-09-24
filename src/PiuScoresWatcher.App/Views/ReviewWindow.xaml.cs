using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Views;

/// <summary>
///     A frame the watcher kept (D38): the picture, what went wrong in a sentence, and where it came from.
///     Nothing here leaves the machine — "Show the file" opens Explorer on it (D37).
/// </summary>
public partial class ReviewWindow : Window
{
    private readonly FailedScreenStore _failed;
    private readonly IClock _clock;
    private KeptScreen? _showing;

    public ReviewWindow(FailedScreenStore failed, IClock clock)
    {
        _failed = failed;
        _clock = clock;
        InitializeComponent();
        ShowScreen(null);
        _failed.Changed += OnKeptChanged;
        Closed += (_, _) => _failed.Changed -= OnKeptChanged;
    }

    /// <summary>Shows the kept frame at <paramref name="imagePath" /> — the one a notification was about — or else the newest.</summary>
    public void ShowScreen(string? imagePath)
    {
        var kept = _failed.List();
        _showing = kept.FirstOrDefault(screen => string.Equals(screen.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                   ?? kept.FirstOrDefault();
        Render(kept.Count);
    }

    private void OnKeptChanged(object? sender, EventArgs e)
    {
        // another frame kept while this one is open: stay on it, but count the new one
        Dispatcher.BeginInvoke(() => ShowScreen(_showing?.ImagePath));
    }

    private void Render(int count)
    {
        var screen = _showing;
        NothingText.Visibility = screen is null ? Visibility.Visible : Visibility.Collapsed;
        ScreenPanel.Visibility = screen is null ? Visibility.Collapsed : Visibility.Visible;
        if (screen is null)
            return;

        HeadingText.Text = Copy.ReviewTitle(screen.Because);
        ReasonText.Text = Copy.ReviewReason(screen.Because);
        DeleteAllLine.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        DeleteAllText.Text = Copy.DeleteAll(count);
        try
        {
            var (width, height) = PixelSize(screen.ImagePath);
            Shot.Source = Thumbnail(screen.ImagePath);
            SeenText.Text = Copy.ReviewSeen(screen.Because, screen.Source, width, height, screen.SeenAt, _clock.Now);
        }
        catch (Exception failure) when (failure is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // deleted or damaged behind our back; the next refresh drops it from the list
            Shot.Source = null;
            SeenText.Text = "";
        }
    }

    private static (int Width, int Height) PixelSize(string path)
    {
        using var file = File.OpenRead(path);
        var frame = BitmapDecoder.Create(file, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>Decoded small and loaded whole, so the file is not held open and Delete can remove it.</summary>
    private static BitmapImage Thumbnail(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.DecodePixelWidth = 480;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void OnShowFile(object sender, RoutedEventArgs e)
    {
        if (_showing is { } screen && File.Exists(screen.ImagePath))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{screen.ImagePath}\"") { UseShellExecute = true });
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_showing is not { } screen)
            return;
        _failed.Changed -= OnKeptChanged;
        _failed.Delete(screen);
        _failed.Changed += OnKeptChanged;
        ShowScreen(null);
        if (_showing is null)
            Close();
    }

    private void OnDeleteAll(object sender, RoutedEventArgs e)
    {
        _failed.Changed -= OnKeptChanged;
        foreach (var screen in _failed.List())
            _failed.Delete(screen);
        Close();
    }

    private void OnPrivacy(object sender, RoutedEventArgs e)
    {
        Links.Open(Links.Privacy);
    }
}

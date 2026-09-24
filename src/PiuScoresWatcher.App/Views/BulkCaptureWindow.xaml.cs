using System.Windows;
using System.Windows.Controls;
using PiuScoresWatcher.App.Capture;
using PiuScoresWatcher.App.Sounds;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Views;

/// <summary>
///     The start of a bulk capture (D51): the moves, the three sounds with a button to hear each, what
///     PIU Scores already has, and the sound switch. Start stays off until the chart list and the player's
///     bests are in, because a run without them cannot tell a new best from an old one.
/// </summary>
public partial class BulkCaptureWindow : Window
{
    private readonly BulkCaptureService _bulk;
    private readonly CancellationTokenSource _closing = new();
    private readonly bool _loading;
    private readonly ISettingsStore _settings;
    private readonly CaptureSounds _sounds;
    private BulkPreparation.Ready? _ready;

    public BulkCaptureWindow(BulkCaptureService bulk, CaptureSounds sounds, ISettingsStore settings)
    {
        _bulk = bulk;
        _sounds = sounds;
        _settings = settings;
        InitializeComponent();
        Feedback.Keys(Step2, Copy.BulkStep2, (Style)FindResource("KeyCap"));

        _loading = true;
        SoundsBox.IsChecked = settings.Load().BulkCaptureSounds;
        _loading = false;

        Loaded += async (_, _) => await PrepareAsync();
        Closed += (_, _) =>
        {
            _closing.Cancel();
            _closing.Dispose();
        };
    }

    private async Task PrepareAsync()
    {
        Feedback.Say(BestsText, Copy.BulkChecking, success: null);
        BulkPreparation preparation;
        try
        {
            preparation = await _bulk.PrepareAsync(_closing.Token);
        }
        catch (OperationCanceledException)
        {
            return; // closed while checking
        }

        switch (preparation)
        {
            case BulkPreparation.Ready ready:
                _ready = ready;
                Feedback.Say(BestsText, Copy.BulkStoredBests(ready.StoredBestCount), success: null);
                StartButton.IsEnabled = !_bulk.IsRunning;
                break;
            case BulkPreparation.NotConnected:
                Feedback.Say(BestsText, Copy.BulkNotConnected, success: false);
                break;
            default:
                Feedback.Say(BestsText, Copy.BulkCouldNotLoad, success: false);
                break;
        }
    }

    private void OnPreview(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CaptureSound sound })
            _sounds.Preview(sound);
    }

    private void OnSoundsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _settings.Save(_settings.Load() with { BulkCaptureSounds = SoundsBox.IsChecked == true });
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (_ready is null)
            return;
        _bulk.Start(_ready);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

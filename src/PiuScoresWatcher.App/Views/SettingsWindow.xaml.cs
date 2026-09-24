using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Capture;
using PiuScoresWatcher.App.Startup;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Views;

/// <summary>One row of Recent, as the list binds it: a play, or a bulk capture run with only its name and count.</summary>
public sealed record RecentRow(string Song, string Chart, string Score, string Grade, bool IsGold, string Mark, bool Recorded, string Age, bool IsRun = false);

/// <summary>
///     The settings window (D37): what the watcher is doing, the account, how it watches, bulk capture
///     (D51), start-up, the plays since it started, anything kept for review, and the notification
///     switches (D35). Every change saves the moment it is made; there is no Save button.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>The grades the mock shows in gold: S and above on RISE, AAA and above on the Arcade Station.</summary>
    private const int GoldFrom = 950_000;

    private readonly ISettingsStore _settings;
    private readonly WatcherStatus _status;
    private readonly BulkCaptureService _bulk;
    private readonly Connection _connection;
    private readonly StartupRegistration _startup;
    private readonly FailedScreenStore _failed;
    private readonly IClock _clock;
    private readonly LaunchOptions _options;
    private readonly DispatcherTimer _ages;
    private readonly bool _loading;

    public SettingsWindow(ISettingsStore settings, WatcherStatus status, BulkCaptureService bulk, Connection connection,
        StartupRegistration startup, FailedScreenStore failed, IClock clock, LaunchOptions options)
    {
        _settings = settings;
        _status = status;
        _bulk = bulk;
        _connection = connection;
        _startup = startup;
        _failed = failed;
        _clock = clock;
        _options = options;
        InitializeComponent();
        // tall enough for everything on a big screen, never taller than the one it opens on
        MaxHeight = SystemParameters.WorkArea.Height - 48;

        Feedback.Link(TokenHelpText, Copy.TokenHelp, Copy.TokenPageLink(options.EffectiveBaseUrl), OpenTokenPage);
        _loading = true;
        var current = settings.Load();
        ModeGame.IsChecked = current.Mode == CaptureMode.Game;
        ModeSteam.IsChecked = current.Mode == CaptureMode.SteamScreenshots;
        ModeBoth.IsChecked = current.Mode == CaptureMode.Both;
        StartWithWindowsBox.IsChecked = current.StartWithWindows;
        BulkSoundsBox.IsChecked = current.BulkCaptureSounds;
        var notifications = current.EffectiveNotifications;
        NotificationsBox.IsChecked = notifications.Enabled;
        NotifyRecordedBox.IsChecked = notifications.Recorded;
        NotifyNotRecordedBox.IsChecked = notifications.NotRecorded;
        NotifyUnreadableBox.IsChecked = notifications.Unreadable;
        NotifyTokenRejectedBox.IsChecked = notifications.TokenRejected;
        NotifyUpdatedBox.IsChecked = notifications.Updated;
        NotifyBulkFinishedBox.IsChecked = notifications.BulkCaptureFinished;
        NotificationKinds.IsEnabled = notifications.Enabled;
        _loading = false;

        RefreshFolder();
        Refresh();
        _status.Changed += OnStatusChanged;
        _failed.Changed += OnStatusChanged;
        // "2 min ago" has to keep moving while the window is open
        _ages = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        Closed += (_, _) =>
        {
            _ages.Stop();
            _status.Changed -= OnStatusChanged;
            _failed.Changed -= OnStatusChanged;
        };

        if (_status.HasToken && _status.Player is null)
            _ = _connection.CheckStoredAsync(CancellationToken.None);
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        var now = _clock.Now;
        if (_status.Bulk is { } run)
        {
            // a run on: the card counts it, and its button stops it
            StatusHeadline.Text = Copy.BulkOn(run);
            StatusDetail.Text = Copy.BulkDetail(run);
            StatusDot.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
            PauseButton.Content = Copy.Stop;
        }
        else
        {
            StatusHeadline.Text = _status.Headline;
            StatusDetail.Text = Copy.StatusDetail(_status.LastRecorded, _status.PlaysToday, now);
            StatusDot.SetResourceReference(Shape.FillProperty, StatusDotBrush());
            PauseButton.Content = _status.Paused ? Copy.Resume : Copy.Pause;
        }

        StartBulkButton.IsEnabled = _status.Bulk is null;

        var connected = _status.HasToken && !_status.TokenRejected;
        ConnectedPanel.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        ConnectPanel.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        if (_status.Player is { } player)
            Feedback.Bold(ConnectedText, Copy.ConnectedAs, player.Username);
        else
            ConnectedText.Text = Copy.ConnectedUnchecked;

        var rows = _status.Recent.Select(recent => Row(recent, now)).ToList();
        RecentList.ItemsSource = rows;
        NoPlays.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var kept = _failed.List();
        var unreadable = kept.Count(screen => !screen.Because.WasRead());
        ReviewRow.Visibility = kept.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ReviewText.Text = Copy.ToReview(unreadable, kept.Count - unreadable);

        VersionText.Text = Copy.VersionLine(AppVersion.Short, _status.UpToDate);
    }

    private string StatusDotBrush()
    {
        if (!_status.HasToken || _status.TokenRejected)
            return "SystemFillColorCriticalBrush";
        if (_status.Paused)
            return "SystemFillColorCautionBrush";
        return _status.GameRunning ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush";
    }

    private static RecentRow Row(RecentEntry entry, DateTimeOffset now)
    {
        if (entry is RecentRun run)
            return new RecentRow(Copy.BulkRunName, Copy.BulkRunDetail(run.Tally), "", "", false, "", true, Copy.AgoShort(run.At, now), IsRun: true);

        var recent = (RecentPlay)entry;
        var play = recent.Play;
        var mark = !recent.Recorded ? Copy.NotRecordedShort
            : Awards.Of(play.Mix, play.Judgments, play.IsBroken) is { } award ? Copy.AwardName(play.Mix, award)
            : play.IsBroken ? Copy.Broken
            : Copy.NoMark;
        return new RecentRow(play.SongName, " · " + Copy.ChartLabel(play.Mix, play.ChartType, play.Level), Copy.Score(play.Score),
            Grades.Of(play.Mix, play.Score), play.Score >= GoldFrom, mark, recent.Recorded, Copy.AgoShort(recent.At, now));
    }

    /// <summary>The Steam screenshots folder: hidden when only the game window is watched, "chosen" when the player picked one.</summary>
    private void RefreshFolder()
    {
        var current = _settings.Load();
        FolderPanel.Visibility = current.Mode == CaptureMode.Game ? Visibility.Collapsed : Visibility.Visible;
        var root = SteamPaths.Root();
        var folders = SteamScreenshotFolders.Resolve(root, SteamPaths.AccountFolders(root), current.SteamScreenshotsFolder);
        var path = folders.FirstOrDefault(Directory.Exists) ?? folders.FirstOrDefault();
        FolderLine.Text = Copy.FolderLine(!string.IsNullOrWhiteSpace(current.SteamScreenshotsFolder), path is not null && Directory.Exists(path));
        FolderPath.Text = path ?? "";
        FolderPath.Visibility = path is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPause(object sender, RoutedEventArgs e)
    {
        if (_status.Bulk is not null)
            _bulk.Stop("stopped from settings");
        else
            _status.SetPaused(!_status.Paused);
    }

    private void OnStartBulk(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowBulkCapture();
    }

    private void OnBulkSoundsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _settings.Save(_settings.Load() with { BulkCaptureSounds = BulkSoundsBox.IsChecked == true });
    }

    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TokenBox.Password))
            return;
        ConnectButton.IsEnabled = false;
        Feedback.Say(ConnectionText, Copy.Checking, success: null);
        var check = await _connection.ConnectAsync(TokenBox.Password, CancellationToken.None);
        ConnectButton.IsEnabled = true;
        switch (check)
        {
            case IdentityCheck.Connected:
                TokenBox.Clear();
                ConnectionText.Visibility = Visibility.Collapsed;
                break;
            case IdentityCheck.Unauthorized:
                Feedback.Say(ConnectionText, Copy.TokenNotAccepted, success: false);
                break;
            default:
                Feedback.Say(ConnectionText, Copy.TokenUnchecked, success: false);
                break;
        }
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        _connection.Disconnect();
    }

    private void OpenTokenPage()
    {
        Links.Open(Links.TokenPage(_options.EffectiveBaseUrl).ToString());
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var mode = ModeGame.IsChecked == true ? CaptureMode.Game
            : ModeSteam.IsChecked == true ? CaptureMode.SteamScreenshots
            : CaptureMode.Both;
        _settings.Save(_settings.Load() with { Mode = mode });
        RefreshFolder();
    }

    private void OnChangeFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(FolderPath.Text))
            dialog.InitialDirectory = FolderPath.Text;
        if (dialog.ShowDialog(this) != true)
            return;
        _settings.Save(_settings.Load() with { SteamScreenshotsFolder = dialog.FolderName });
        RefreshFolder();
    }

    private void OnStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var startWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.Save(_settings.Load() with { StartWithWindows = startWithWindows });
        _startup.Apply(startWithWindows);
    }

    private void OnNotificationsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var enabled = NotificationsBox.IsChecked == true;
        NotificationKinds.IsEnabled = enabled;
        _settings.Save(_settings.Load() with
        {
            Notifications = new NotificationSettings(enabled, NotifyRecordedBox.IsChecked == true, NotifyNotRecordedBox.IsChecked == true,
                NotifyUnreadableBox.IsChecked == true, NotifyTokenRejectedBox.IsChecked == true, NotifyUpdatedBox.IsChecked == true,
                NotifyBulkFinishedBox.IsChecked == true)
        });
    }

    private void OnReview(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowReview();
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.Logs}\"") { UseShellExecute = true });
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

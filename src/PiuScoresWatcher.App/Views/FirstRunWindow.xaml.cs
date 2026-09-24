using System.Windows;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Startup;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Views;

/// <summary>
///     Shown whenever no token is stored (D39): how it works first (D65), then, from "Set it up", the token,
///     checked on the spot; how to watch; Start with Windows. Done saves the choices and closes into the tray —
///     connecting a token that was pasted but never connected first, and staying open when it does not connect.
/// </summary>
public partial class FirstRunWindow : Window
{
    private readonly Connection _connection;
    private readonly ISettingsStore _settings;
    private readonly StartupRegistration _startup;
    private readonly LaunchOptions _options;
    private bool _connected;

    public FirstRunWindow(Connection connection, ISettingsStore settings, StartupRegistration startup, WatcherStatus status, LaunchOptions options)
    {
        _connection = connection;
        _settings = settings;
        _startup = startup;
        _options = options;
        InitializeComponent();

        Feedback.Link(TokenHelpText, Copy.TokenHelp, Copy.TokenPageLink(options.EffectiveBaseUrl), OpenTokenPage);
        var current = settings.Load();
        ModeGame.IsChecked = current.Mode == CaptureMode.Game;
        ModeSteam.IsChecked = current.Mode == CaptureMode.SteamScreenshots;
        ModeBoth.IsChecked = current.Mode == CaptureMode.Both;
        StartWithWindowsBox.IsChecked = current.StartWithWindows;
        if (status.Player is { } player)
            ShowConnected(player);
    }

    /// <summary>From how it works to the three steps; Enter now means Done, and the token box is ready for a paste.</summary>
    private void OnSetItUp(object sender, RoutedEventArgs e)
    {
        IntroPage.Visibility = Visibility.Collapsed;
        SetupPage.Visibility = Visibility.Visible;
        SetItUpButton.IsDefault = false;
        DoneButton.IsDefault = true;
        TokenBox.Focus();
    }

    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        await ConnectAsync();
    }

    /// <summary>Checks the pasted token and says how that went; true when it is connected.</summary>
    private async Task<bool> ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(TokenBox.Password))
            return false;
        ConnectButton.IsEnabled = false;
        DoneButton.IsEnabled = false;
        Say(Copy.Checking, success: null);
        var check = await _connection.ConnectAsync(TokenBox.Password, CancellationToken.None);
        ConnectButton.IsEnabled = true;
        DoneButton.IsEnabled = true;
        switch (check)
        {
            case IdentityCheck.Connected connected:
                ShowConnected(connected.Player);
                return true;
            case IdentityCheck.Unauthorized:
                Say(Copy.TokenNotAccepted, success: false);
                return false;
            default:
                Say(Copy.TokenUnchecked, success: false);
                return false;
        }
    }

    private void ShowConnected(PlayerIdentity player)
    {
        _connected = true;
        Say(Copy.ConnectedAs, success: true);
        Feedback.Bold(ConnectionText, Copy.ConnectedAs, player.Username);
    }

    private void Say(string text, bool? success)
    {
        Feedback.Say(ConnectionText, text, success);
        ConnectionLine.Visibility = Visibility.Visible;
        CheckIcon.Visibility = success == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnDone(object sender, RoutedEventArgs e)
    {
        // a pasted token nobody pressed Connect for would otherwise close into a watcher that records nothing
        if (!_connected && !string.IsNullOrWhiteSpace(TokenBox.Password) && (!await ConnectAsync() || !IsVisible))
            return; // not connected, and the line under the token says why — or the window was closed meanwhile

        var mode = ModeGame.IsChecked == true ? CaptureMode.Game
            : ModeSteam.IsChecked == true ? CaptureMode.SteamScreenshots
            : CaptureMode.Both;
        var startWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.Save(_settings.Load() with { Mode = mode, StartWithWindows = startWithWindows });
        _startup.Apply(startWithWindows);
        Close();
    }

    private void OpenTokenPage()
    {
        Links.Open(Links.TokenPage(_options.EffectiveBaseUrl).ToString());
    }

    private void OnPrivacy(object sender, RoutedEventArgs e)
    {
        Links.Open(Links.Privacy);
    }
}

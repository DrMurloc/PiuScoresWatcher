using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Capture;
using PiuScoresWatcher.App.Localization;
using PiuScoresWatcher.App.Notifications;
using PiuScoresWatcher.App.Ocr;
using PiuScoresWatcher.App.Security;
using PiuScoresWatcher.App.Sounds;
using PiuScoresWatcher.App.Startup;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.App.Time;
using PiuScoresWatcher.App.Updates;
using PiuScoresWatcher.App.Views;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;
using PiuScoresWatcher.Core.Time;
using Serilog;

namespace PiuScoresWatcher.App;

/// <summary>
///     The process: a generic host carrying the services and the background work, a tray icon whose
///     menu leads with what the watcher is doing, and the windows on demand — first run while no token
///     is stored (D39), settings, the review window, and the start of a bulk capture (D51).
/// </summary>
public partial class App : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstance _instance;
    private IHost? _host;
    private TaskbarIcon? _tray;
    private ContextMenu? _menu;
    private MenuItem? _statusItem;
    private MenuItem? _pauseItem;
    private MenuItem? _startBulkItem;
    private MenuItem? _stopBulkItem;
    private MenuItem? _openItem;
    private MenuItem? _siteItem;
    private MenuItem? _quitItem;
    private FirstRunWindow? _firstRun;
    private SettingsWindow? _settings;
    private ReviewWindow? _review;
    private BulkCaptureWindow? _bulkCapture;

    internal App(LaunchOptions options, SingleInstance instance)
    {
        _options = options;
        _instance = instance;
    }

    private IServiceProvider Services => _host!.Services;

    private WatcherStatus Status => Services.GetRequiredService<WatcherStatus>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();
        _host = BuildHost();
        // the language first: the services that start next can raise a notification straight away (D59)
        WatcherLanguage.Apply(Services.GetRequiredService<ISettingsStore>().Load().Language);
        _host.Start();

        var log = Services.GetRequiredService<ILogger<App>>();
        log.LogInformation("PIU Scores Watcher {Version} started against {BaseUrl}", AppVersion.Informational, _options.EffectiveBaseUrl);
        // A tray app that dies on a window's mistake stops recording plays; log it and carry on.
        DispatcherUnhandledException += (_, failure) =>
        {
            log.LogError(failure.Exception, "A window failed");
            failure.Handled = true;
        };

        _tray = CreateTray();
        Status.Changed += (_, _) => Dispatcher.BeginInvoke(RefreshTray);
        _instance.Listen(() => Dispatcher.BeginInvoke(ShowSettings));
        // A click on a notification reaches this process whether it is running or Windows starts it for the click.
        ToastNotificationManagerCompat.OnActivated += activation =>
        {
            var arguments = ToastArguments.Parse(activation.Argument);
            Dispatcher.BeginInvoke(() => OnNotificationClicked(arguments));
        };

        var settings = Services.GetRequiredService<ISettingsStore>();
        AnnounceUpdate(settings);
        Services.GetRequiredService<StartupRegistration>().Apply(settings.Load().StartWithWindows);
        if (string.IsNullOrWhiteSpace(Services.GetRequiredService<ITokenStore>().Load()))
            ShowFirstRun();
        else
            _ = Services.GetRequiredService<Connection>().CheckStoredAsync(CancellationToken.None);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private IHost BuildHost()
    {
        // Content root is the install folder, not whatever directory the shortcut or shell started us
        // in; and a tray app has no console for the lifetime's "press Ctrl+C" line to mean anything.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        builder.Services.AddSerilog(logger => logger
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(AppPaths.Logs, "watcher-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));
        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        builder.Services.AddSingleton<DpapiTokenStore>();
        builder.Services.AddSingleton<ITokenStore>(services => new EnvironmentOrStoredToken(services.GetRequiredService<DpapiTokenStore>()));
        builder.Services.AddSingleton(services => PiuScoresHttp.Client(_options, services.GetRequiredService<ITokenStore>()));
        builder.Services.AddSingleton<ResultScreenDetector>();
        builder.Services.AddSingleton<ResultScreenReader>();
        builder.Services.AddSingleton<ITitleReader, WindowsOcrTitleReader>();
        builder.Services.AddSingleton<Deduplicator>();
        builder.Services.AddSingleton<FailedScreenStore>();
        builder.Services.AddSingleton<IFailedScreenStore>(services => services.GetRequiredService<FailedScreenStore>());
        builder.Services.AddSingleton<WatcherStatus>();
        builder.Services.AddSingleton<INotifier, WatcherNotifier>();
        builder.Services.AddSingleton<Connection>();
        builder.Services.AddSingleton<StartupRegistration>();
        builder.Services.AddSingleton<SongCatalogs>();
        builder.Services.AddSingleton<ISongCatalogs>(services => services.GetRequiredService<SongCatalogs>());
        builder.Services.AddHostedService(services => services.GetRequiredService<SongCatalogs>());
        builder.Services.AddSingleton<CapturePipeline>();
        builder.Services.AddSingleton<RiseProcessWatch>();
        builder.Services.AddSingleton<IGameSession>(services => services.GetRequiredService<RiseProcessWatch>());
        builder.Services.AddHostedService(services => services.GetRequiredService<RiseProcessWatch>());
        builder.Services.AddSingleton<WindowCaptureSource>();
        builder.Services.AddSingleton<Func<SteamScreenshotSource>>(services => () =>
        {
            var root = SteamPaths.Root();
            var folders = SteamScreenshotFolders.Resolve(root, SteamPaths.AccountFolders(root),
                services.GetRequiredService<ISettingsStore>().Load().SteamScreenshotsFolder);
            return new SteamScreenshotSource(folders, services.GetRequiredService<ILogger<SteamScreenshotSource>>());
        });
        builder.Services.AddSingleton<CaptureSounds>();
        builder.Services.AddSingleton<BulkCaptureService>();
        builder.Services.AddHostedService<CaptureService>();
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddTransient<FirstRunWindow>();
        builder.Services.AddTransient<SettingsWindow>();
        builder.Services.AddTransient<ReviewWindow>();
        builder.Services.AddTransient<BulkCaptureWindow>();
        return builder.Build();
    }

    /// <summary>
    ///     The first launch of a new version says so (D42). Velopack applies an update before any window
    ///     can exist, so the version the settings last saw is what tells an update from a first install.
    /// </summary>
    private void AnnounceUpdate(ISettingsStore settings)
    {
        var current = settings.Load();
        if (current.LastSeenVersion == AppVersion.Short)
            return;
        if (current.LastSeenVersion is not null)
            Services.GetRequiredService<INotifier>().Notify(new WatcherNotice.Updated(AppVersion.Short));
        settings.Save(current with { LastSeenVersion = AppVersion.Short });
    }

    /// <summary>
    ///     The tray icon and its menu: what the watcher is doing, then what the player can do about it.
    ///     During a bulk capture the menu leads with its count, Stop takes Start's place at the top, and
    ///     Pause steps aside (D51).
    /// </summary>
    private TaskbarIcon CreateTray()
    {
        _statusItem = new MenuItem { IsEnabled = false };
        _pauseItem = new MenuItem();
        _pauseItem.Click += (_, _) => Status.SetPaused(!Status.Paused);
        _startBulkItem = new MenuItem();
        _startBulkItem.Click += (_, _) => ShowBulkCapture();
        _stopBulkItem = new MenuItem();
        _stopBulkItem.Click += (_, _) => Services.GetRequiredService<BulkCaptureService>().Stop("stopped from the tray");
        _openItem = new MenuItem();
        _openItem.Click += (_, _) => ShowSettings();
        _siteItem = new MenuItem();
        _siteItem.Click += (_, _) => OpenSite();
        _quitItem = new MenuItem();
        _quitItem.Click += (_, _) => Shutdown();

        _menu = new ContextMenu();
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_stopBulkItem);
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_startBulkItem);
        _menu.Items.Add(_siteItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_quitItem);

        var tray = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = _menu,
            NoLeftClickDelay = true
        };
        tray.TrayLeftMouseUp += (_, _) => ShowSettings();
        // An icon made in code is not in any visual tree, so it has to be created explicitly; and not in
        // efficiency mode, which would slow the capture loop whenever no window is open.
        tray.ForceCreate(enablesEfficiencyMode: false);
        RefreshTray();
        return tray;
    }

    /// <summary>The menu's words and the tooltip, in the current language — so a language change relabels them too.</summary>
    private void RefreshTray()
    {
        if (_tray is null || _menu is null || _statusItem is null || _pauseItem is null || _startBulkItem is null || _stopBulkItem is null
            || _openItem is null || _siteItem is null || _quitItem is null)
            return;
        var headline = Status.Headline;
        var bulk = Status.Bulk is not null;
        _menu.Language = WatcherLanguage.Xml;
        _statusItem.Header = headline;
        _startBulkItem.Header = Copy.TrayStartBulk;
        _stopBulkItem.Header = Copy.TrayStopBulk;
        _openItem.Header = Copy.TrayOpenSettings;
        _siteItem.Header = Copy.TrayOpenSite;
        _quitItem.Header = Copy.Quit;
        _pauseItem.Header = Status.Paused ? Copy.TrayResume : Copy.TrayPause;
        _pauseItem.Visibility = bulk ? Visibility.Collapsed : Visibility.Visible;
        _startBulkItem.Visibility = bulk ? Visibility.Collapsed : Visibility.Visible;
        _stopBulkItem.Visibility = bulk ? Visibility.Visible : Visibility.Collapsed;
        _tray.ToolTipText = $"{Copy.AppNameFor(_options.Scope)} — {headline}";
    }

    internal void ShowFirstRun()
    {
        _firstRun ??= Open<FirstRunWindow>(() => _firstRun = null);
        Bring(_firstRun);
    }

    internal void ShowSettings()
    {
        _settings ??= OpenSettings();
        Bring(_settings);
    }

    /// <summary>
    ///     The Language picker's choice (D59): the settings window redrawn where it stood and as far down as it was
    ///     scrolled, the tray relabelled. Nothing restarts; another open window follows when it next opens.
    /// </summary>
    internal void ChangeLanguage(string? chosen)
    {
        WatcherLanguage.Apply(chosen);
        RefreshTray();
        if (_settings is not { } before)
            return;

        var (left, top, scrolled) = (before.Left, before.Top, before.ScrollFraction);
        before.Close();
        _settings = OpenSettings();
        _settings.WindowStartupLocation = WindowStartupLocation.Manual;
        _settings.Left = left;
        _settings.Top = top;
        _settings.ScrollTo(scrolled);
        Bring(_settings);
    }

    private SettingsWindow OpenSettings()
    {
        SettingsWindow? window = null;
        // only this window's closing forgets it: a redrawn one replaces it before the old one's Closed is done
        window = Open<SettingsWindow>(() =>
        {
            if (ReferenceEquals(_settings, window))
                _settings = null;
        });
        return window;
    }

    /// <summary>The start of a bulk capture; while one runs, settings and the tray offer Stop instead.</summary>
    internal void ShowBulkCapture()
    {
        if (_bulkCapture is null)
        {
            _bulkCapture = Open<BulkCaptureWindow>(() => _bulkCapture = null);
            _bulkCapture.Title = Copy.AppNameFor(_options.Scope, Copy.BulkWindowName);
        }

        Bring(_bulkCapture);
    }

    /// <summary>The review window, on the frame a notification was about when there is one.</summary>
    internal void ShowReview(string? imagePath = null)
    {
        _review ??= Open<ReviewWindow>(() => _review = null);
        _review.ShowScreen(imagePath);
        Bring(_review);
    }

    private void OnNotificationClicked(ToastArguments arguments)
    {
        arguments.TryGetValue(ToastAction.Key, out string? action);
        switch (action)
        {
            case ToastAction.Review:
                ShowReview(arguments.TryGetValue(ToastAction.Path, out string? path) ? path : null);
                break;
            case ToastAction.Release:
                Links.Open(Links.Release(arguments.TryGetValue(ToastAction.Version, out string? version) ? version : AppVersion.Short));
                break;
            case ToastAction.Site:
                OpenSite();
                break;
            default:
                ShowSettings();
                break;
        }
    }

    private T Open<T>(Action whenClosed) where T : Window
    {
        var window = Services.GetRequiredService<T>();
        // a dev run names its site, so its windows are never mistaken for the installed copy's (D44)
        window.Title = Copy.AppNameFor(_options.Scope);
        window.Language = WatcherLanguage.Xml;
        window.Closed += (_, _) => whenClosed();
        return window;
    }

    private static void Bring(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void OpenSite()
    {
        Process.Start(new ProcessStartInfo(_options.EffectiveBaseUrl.ToString()) { UseShellExecute = true });
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Capture;
using PiuScoresWatcher.App.Notifications;
using PiuScoresWatcher.App.Ocr;
using PiuScoresWatcher.App.Security;
using PiuScoresWatcher.App.Startup;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.App.Time;
using PiuScoresWatcher.App.Updates;
using PiuScoresWatcher.App.Views;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;
using PiuScoresWatcher.Core.Time;
using Serilog;

namespace PiuScoresWatcher.App;

/// <summary>
///     The process: a generic host carrying the services and the background work, a tray icon whose
///     menu leads with what the watcher is doing, and the windows on demand.
/// </summary>
public partial class App : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstance _instance;
    private IHost? _host;
    private TaskbarIcon? _tray;
    private MenuItem? _statusItem;
    private MenuItem? _pauseItem;
    private SettingsWindow? _settings;

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
        _host.Start();

        var log = Services.GetRequiredService<ILogger<App>>();
        log.LogInformation("PIU Scores Watcher {Version} started against {BaseUrl}", AppVersion.Informational, _options.EffectiveBaseUrl);

        _tray = CreateTray();
        Status.Changed += (_, _) => Dispatcher.BeginInvoke(RefreshTray);
        _instance.Listen(() => Dispatcher.BeginInvoke(ShowSettings));
        _ = CheckConnectionAsync();
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
        builder.Services.AddSingleton<IFailedScreenStore, FailedScreenStore>();
        builder.Services.AddSingleton<WatcherStatus>();
        builder.Services.AddSingleton<INotifier, WatcherNotifier>();
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
        builder.Services.AddHostedService<CaptureService>();
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddTransient<SettingsWindow>();
        return builder.Build();
    }

    /// <summary>The tray icon and its menu: what the watcher is doing, then what the player can do about it.</summary>
    private TaskbarIcon CreateTray()
    {
        _statusItem = new MenuItem { IsEnabled = false };
        _pauseItem = new MenuItem();
        _pauseItem.Click += (_, _) => Status.SetPaused(!Status.Paused);
        var open = new MenuItem { Header = Copy.TrayOpenSettings };
        open.Click += (_, _) => ShowSettings();
        var site = new MenuItem { Header = Copy.TrayOpenSite };
        site.Click += (_, _) => OpenSite();
        var quit = new MenuItem { Header = Copy.Quit };
        quit.Click += (_, _) => Shutdown();

        var menu = new ContextMenu();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(site);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        var tray = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = menu,
            NoLeftClickDelay = true
        };
        tray.TrayLeftMouseUp += (_, _) => ShowSettings();
        // An icon made in code is not in any visual tree, so it has to be created explicitly; and not in
        // efficiency mode, which would slow the capture loop whenever no window is open.
        tray.ForceCreate(enablesEfficiencyMode: false);
        RefreshTray();
        return tray;
    }

    private void RefreshTray()
    {
        if (_tray is null || _statusItem is null || _pauseItem is null)
            return;
        var headline = Status.Headline;
        _statusItem.Header = headline;
        _pauseItem.Header = Status.Paused ? Copy.TrayResume : Copy.TrayPause;
        _tray.ToolTipText = $"{Copy.AppName} — {headline}";
    }

    /// <summary>Who the stored token belongs to, asked once at start-up; a token that fails leaves the status "not connected".</summary>
    private async Task CheckConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(Services.GetRequiredService<ITokenStore>().Load()))
            return;
        if (await Services.GetRequiredService<IPlaysClient>().WhoAmIAsync(CancellationToken.None) is IdentityCheck.Connected connected)
            Status.Connected(connected.Player);
    }

    internal void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = Services.GetRequiredService<SettingsWindow>();
            _settings.Closed += (_, _) => _settings = null;
        }

        _settings.Show();
        _settings.Activate();
    }

    private void OpenSite()
    {
        Process.Start(new ProcessStartInfo(_options.EffectiveBaseUrl.ToString()) { UseShellExecute = true });
    }
}

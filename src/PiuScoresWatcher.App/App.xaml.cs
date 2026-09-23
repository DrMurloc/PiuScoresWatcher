using System.IO;
using System.Windows;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Security;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.App.Time;
using PiuScoresWatcher.App.Updates;
using PiuScoresWatcher.App.Views;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;
using PiuScoresWatcher.Core.Time;
using Serilog;

namespace PiuScoresWatcher.App;

/// <summary>
///     The process: a generic host carrying the services and the background work, a tray icon
///     that outlives every window, and the settings window on demand.
/// </summary>
public partial class App : Application
{
    private readonly LaunchOptions _options;
    private IHost? _host;
    private TaskbarIcon? _tray;
    private SettingsWindow? _settings;

    public App(LaunchOptions options)
    {
        _options = options;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();

        // Content root is the install folder, not whatever directory the shortcut or shell started us
        // in; and a tray app has no console for the lifetime's "press Ctrl+C" line to mean anything.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        builder.Services.AddSerilog(logger => logger
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(AppPaths.Logs, "watcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7));
        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        builder.Services.AddSingleton<DpapiTokenStore>();
        builder.Services.AddSingleton<ITokenStore>(services => new EnvironmentOrStoredToken(services.GetRequiredService<DpapiTokenStore>()));
        builder.Services.AddSingleton(services => PiuScoresHttp.Client(_options, services.GetRequiredService<ITokenStore>()));
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddTransient<SettingsWindow>();

        _host = builder.Build();
        _host.Start();

        var log = _host.Services.GetRequiredService<ILogger<App>>();
        log.LogInformation("PIU Scores Watcher {Version} started against {BaseUrl}",
            AppVersion.Informational, _options.EffectiveBaseUrl);
        if (_options.ReplayFile is not null)
            log.LogWarning("--replay {File} requested, but the pipeline is not built yet", _options.ReplayFile);

        _tray = (TaskbarIcon)FindResource("TrayIcon");
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

    private void OnTrayLeftClick(object sender, RoutedEventArgs e)
    {
        ShowSettings();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        ShowSettings();
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }

    private void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = _host!.Services.GetRequiredService<SettingsWindow>();
            _settings.Closed += (_, _) => _settings = null;
        }

        _settings.Show();
        _settings.Activate();
    }
}

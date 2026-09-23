using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using PiuScoresWatcher.App.Startup;
using PiuScoresWatcher.Core.Exceptions;
using PiuScoresWatcher.Core.Startup;
using Velopack;

namespace PiuScoresWatcher.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack first: on install, update and uninstall it runs its hooks and exits before any
        // window exists, which is why startup is not in App.OnStartup. Uninstalling takes the Start
        // with Windows entry and the notification registration with it (D40).
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ =>
            {
                StartupRegistration.Remove();
                ToastNotificationManagerCompat.Uninstall();
            })
            .Run();

        LaunchOptions options;
        try
        {
            options = LaunchOptions.Parse(args, Environment.GetEnvironmentVariable(LaunchOptions.BaseUrlVariable));
        }
        catch (InvalidLaunchOptionsException refusal)
        {
            // A dev seam misused: say so and stop, rather than run against production by accident.
            MessageBox.Show(refusal.Message, Copy.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }

        // A replay is a command, not the tray app: it runs beside a running watcher (D39).
        if (options.ReplayFile is not null)
            return Replay.ReplayRunner.Run(options);

        // A second launch asks the running watcher to open its settings, and leaves.
        using var instance = SingleInstance.Claim();
        if (instance is null)
            return 0;

        var app = new App(options, instance);
        app.InitializeComponent();
        return app.Run();
    }
}

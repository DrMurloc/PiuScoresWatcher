using System.Windows;
using PiuScoresWatcher.Core.Exceptions;
using PiuScoresWatcher.Core.Startup;
using Velopack;

namespace PiuScoresWatcher.App;

public static class Program
{
    /// <summary>One watcher per Windows session; the name is local to the session, not the machine.</summary>
    private const string InstanceMutex = @"Local\PiuScoresWatcher";

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack first: on install, update and uninstall it runs its hooks and exits before any
        // window exists, which is why startup is not in App.OnStartup.
        VelopackApp.Build().Run();

        // A second launch — the shortcut double-clicked while the tray icon already exists — exits
        // quietly. Later it will ask the running instance to open its settings instead.
        using var instance = new Mutex(initiallyOwned: true, InstanceMutex, out var first);
        if (!first)
            return 0;

        LaunchOptions options;
        try
        {
            options = LaunchOptions.Parse(args, Environment.GetEnvironmentVariable(LaunchOptions.BaseUrlVariable));
        }
        catch (InvalidLaunchOptionsException refusal)
        {
            // A dev seam misused: say so and stop, rather than run against production by accident.
            MessageBox.Show(refusal.Message, "PIU Scores Watcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }

        var app = new App(options);
        app.InitializeComponent();
        return app.Run();
    }
}

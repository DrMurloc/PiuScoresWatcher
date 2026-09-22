using System.Diagnostics;
using System.Windows;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ISettingsStore settings, LaunchOptions options)
    {
        InitializeComponent();
        VersionText.Text = $"PIU Scores Watcher {AppVersion.Informational}";
        SiteText.Text = options.EffectiveBaseUrl.ToString();
        ModeText.Text = settings.Load().Mode.ToString();
        DataFolderText.Text = AppPaths.Root;
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.Logs) { UseShellExecute = true });
    }
}

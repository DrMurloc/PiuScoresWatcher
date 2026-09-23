using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Is RISE running, and which window is it? Asked every few seconds by name — the Steam build
///     is <c>PUMP IT UP RISE.exe</c> — which is all the watcher ever does with the game's process.
/// </summary>
public sealed class RiseProcessWatch(ILogger<RiseProcessWatch> log) : BackgroundService, IGameSession
{
    public const string ProcessName = "PUMP IT UP RISE";
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private volatile bool _running;
    private nint _mainWindow;

    public bool IsRunning => _running;

    /// <summary>The game's main window, or 0 while it is not running (or has no window yet).</summary>
    public nint MainWindow => _mainWindow;

    public event EventHandler<bool>? RunningChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Look();
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Look()
    {
        var running = false;
        nint window = 0;
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                running = true;
                if (process.MainWindowHandle != 0)
                    window = process.MainWindowHandle;
            }
        }

        _mainWindow = window;
        if (running == _running)
            return;
        _running = running;
        log.LogInformation(running ? "RISE started; watching its window" : "RISE closed; sleeping");
        RunningChanged?.Invoke(this, running);
    }
}

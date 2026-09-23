namespace PiuScoresWatcher.App.Startup;

/// <summary>
///     One watcher per Windows session. A second launch — the Start menu entry clicked while the tray
///     icon already exists — asks the running one to open its settings and exits (D39). Both names are
///     local to the session, not the machine.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\PiuScoresWatcher";
    private const string ShowSettingsName = @"Local\PiuScoresWatcher.ShowSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSettings;
    private volatile bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle showSettings)
    {
        _mutex = mutex;
        _showSettings = showSettings;
    }

    /// <summary>This process's claim on being the watcher, or null when one already runs (and has been asked to show itself).</summary>
    public static SingleInstance? Claim()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var first);
        if (first)
            return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsName));

        mutex.Dispose();
        if (EventWaitHandle.TryOpenExisting(ShowSettingsName, out var running))
            using (running)
                running.Set();
        return null;
    }

    /// <summary>Calls <paramref name="showSettings" /> whenever a second launch asks; on a background thread.</summary>
    public void Listen(Action showSettings)
    {
        new Thread(() =>
        {
            while (_showSettings.WaitOne() && !_disposed)
                showSettings();
        })
        {
            IsBackground = true,
            Name = "Second-launch listener"
        }.Start();
    }

    public void Dispose()
    {
        _disposed = true;
        _showSettings.Set();
        _showSettings.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

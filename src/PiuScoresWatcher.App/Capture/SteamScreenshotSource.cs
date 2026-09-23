using System.IO;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PiuScoresWatcher.App.Replay;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.App.Capture;

/// <summary>Where Steam is installed and which accounts have a userdata folder — the two facts the screenshot folders are built from.</summary>
public static class SteamPaths
{
    public static string? Root()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IEnumerable<string> AccountFolders(string? root)
    {
        var userdata = root is null ? null : Path.Combine(root, "userdata");
        if (userdata is null || !Directory.Exists(userdata))
            return [];
        return Directory.EnumerateDirectories(userdata).Select(Path.GetFileName).Where(name => name is not null)!;
    }
}

/// <summary>
///     Steam-screenshot mode: every JPEG Steam writes into RISE's screenshots folder is decoded and
///     handed to the pipeline once it has finished being written. Costs nothing during play; the
///     player presses F12 on the result screen.
/// </summary>
public sealed class SteamScreenshotSource(IReadOnlyList<string> folders, ILogger<SteamScreenshotSource> log) : IScreenSource
{
    public CaptureSource Kind => CaptureSource.SteamScreenshot;

    public IReadOnlyList<string> Folders => folders;

    public async Task RunAsync(Func<CapturedFrame, Task> onFrame, CancellationToken cancellationToken)
    {
        var arrivals = Channel.CreateUnbounded<string>();
        var watchers = new List<FileSystemWatcher>();
        foreach (var folder in folders.Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(folder, "*.jpg")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Created += (_, e) => arrivals.Writer.TryWrite(e.FullPath);
            watcher.Changed += (_, e) => arrivals.Writer.TryWrite(e.FullPath);
            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
            log.LogInformation("Watching {Folder} for F12 screenshots", folder);
        }

        if (watchers.Count == 0)
            log.LogWarning("No RISE screenshots folder exists yet; F12 mode has nothing to watch");

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await foreach (var path in arrivals.Reader.ReadAllAsync(cancellationToken))
            {
                if (!SteamScreenshotFolders.IsScreenshot(path) || !handled.Add(path))
                    continue;
                if (!await WrittenCompletelyAsync(path, cancellationToken))
                {
                    log.LogWarning("{File} never finished being written", path);
                    continue;
                }

                try
                {
                    var image = WpfScreenDecoder.Decode(path);
                    var takenAt = new DateTimeOffset(File.GetLastWriteTime(path));
                    await onFrame(new CapturedFrame(image, CaptureSource.SteamScreenshot, takenAt, path));
                }
                catch (Exception failure) when (failure is IOException or NotSupportedException or ArgumentException)
                {
                    log.LogWarning(failure, "{File} could not be decoded", path);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        finally
        {
            foreach (var watcher in watchers)
                watcher.Dispose();
        }
    }

    /// <summary>Steam writes the JPEG in more than one step; wait until its size holds still and it opens.</summary>
    private static async Task<bool> WrittenCompletelyAsync(string path, CancellationToken cancellationToken)
    {
        long lastSize = -1;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(250, cancellationToken);
            try
            {
                var size = new FileInfo(path).Length;
                if (size > 0 && size == lastSize)
                {
                    using var probe = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }

                lastSize = size;
            }
            catch (IOException)
            {
                // still being written
            }
        }

        return false;
    }
}

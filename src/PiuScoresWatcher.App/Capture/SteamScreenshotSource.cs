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
///     player presses F12 on the result screen. Steam makes the folder at a player's first F12 in RISE,
///     so a folder that is not there yet is looked for until it is, and a file that will not decode is
///     logged and passed over — neither may end F12 mode for the session.
/// </summary>
public sealed class SteamScreenshotSource(IReadOnlyList<string> folders, ILogger<SteamScreenshotSource> log) : IScreenSource
{
    /// <summary>How often a screenshots folder that does not exist yet is looked for.</summary>
    private static readonly TimeSpan FolderPoll = TimeSpan.FromSeconds(5);

    public CaptureSource Kind => CaptureSource.SteamScreenshot;

    public IReadOnlyList<string> Folders => folders;

    public async Task RunAsync(Func<CapturedFrame, Task> onFrame, CancellationToken cancellationToken)
    {
        var arrivals = Channel.CreateUnbounded<string>();
        var watchers = new List<FileSystemWatcher>();
        var missing = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? lookingForFolders = null;
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var folder in folders)
                if (Directory.Exists(folder))
                    Watch(folder, arrivals.Writer, watchers);
                else
                    missing.Add(folder);

            if (missing.Count > 0)
            {
                log.LogInformation("Waiting for {Folders} to exist; Steam makes it at the first F12 in RISE", string.Join(", ", missing));
                lookingForFolders = WatchForAsync(missing, arrivals.Writer, watchers, stop.Token);
            }

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
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    // a truncated JPEG throws FileFormatException, a locked one UnauthorizedAccessException; one file never ends the mode
                    log.LogWarning(failure, "{File} could not be read", path);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        finally
        {
            // the folder search adds watchers too: it has stopped before they are let go
            await stop.CancelAsync();
            if (lookingForFolders is not null)
                await lookingForFolders;
            foreach (var watcher in watchers)
                watcher.Dispose();
        }
    }

    private void Watch(string folder, ChannelWriter<string> arrivals, List<FileSystemWatcher> watchers)
    {
        var watcher = new FileSystemWatcher(folder, "*.jpg")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watchers.Add(watcher);
        watcher.Created += (_, e) => arrivals.TryWrite(e.FullPath);
        watcher.Changed += (_, e) => arrivals.TryWrite(e.FullPath);
        watcher.EnableRaisingEvents = true;
        log.LogInformation("Watching {Folder} for F12 screenshots", folder);
    }

    /// <summary>
    ///     Looks for the folders that are not there yet until they are. What a folder holds when it appears is read
    ///     too: it did not exist when watching began, so everything in it is new — the F12 that made it among them.
    /// </summary>
    private async Task WatchForAsync(List<string> missing, ChannelWriter<string> arrivals, List<FileSystemWatcher> watchers,
        CancellationToken cancellationToken)
    {
        try
        {
            while (missing.Count > 0)
            {
                await Task.Delay(FolderPoll, cancellationToken);
                foreach (var folder in missing.Where(Directory.Exists).ToList())
                {
                    try
                    {
                        Watch(folder, arrivals, watchers);
                        missing.Remove(folder);
                        foreach (var file in Directory.EnumerateFiles(folder, "*.jpg"))
                            arrivals.TryWrite(file);
                    }
                    catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
                    {
                        log.LogWarning(failure, "{Folder} appeared but could not be watched yet", folder);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
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

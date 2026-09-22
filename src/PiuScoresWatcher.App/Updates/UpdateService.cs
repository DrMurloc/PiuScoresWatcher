using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace PiuScoresWatcher.App.Updates;

/// <summary>
///     Once per launch: ask GitHub Releases for a newer version, download it in the background and
///     leave it staged, so the next start runs the new one. RISE patches monthly and a moved result
///     layout breaks the reader; the fix reaching players without anyone doing anything is the point.
///     A copy that is not installed (a dev run from bin/) skips the whole thing.
/// </summary>
public sealed class UpdateService(ILogger<UpdateService> log) : BackgroundService
{
    private const string Repository = "https://github.com/DrMurloc/PiuScoresWatcher";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var manager = new UpdateManager(new GithubSource(Repository, accessToken: null, prerelease: false));
        if (!manager.IsInstalled)
        {
            log.LogInformation("Not an installed copy; skipping the update check");
            return;
        }

        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                log.LogInformation("Up to date ({Version})", manager.CurrentVersion);
                return;
            }

            await manager.DownloadUpdatesAsync(update, cancelToken: stoppingToken);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            log.LogInformation("Version {Version} downloaded; it applies the next time the watcher starts",
                update.TargetFullRelease.Version);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.LogWarning(failure, "Update check failed; trying again next launch");
        }
    }
}

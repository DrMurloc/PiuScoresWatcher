using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.App.Api;

/// <summary>
///     Both RISE mixes' chart lists, loaded once PIU Scores has said who the token is and refreshed
///     twice a day, so a title the OCR slipped on is posted in the catalog's spelling (D49). Until a
///     list arrives the pipeline posts what it read, exactly as before.
/// </summary>
public sealed class SongCatalogs(IPlaysClient site, WatcherStatus status, ILogger<SongCatalogs> log) : BackgroundService, ISongCatalogs
{
    private static readonly RiseMix[] Mixes = [RiseMix.Rise, RiseMix.RiseArcade];
    private readonly ConcurrentDictionary<RiseMix, SongCatalog> _loaded = new();

    public SongCatalog? For(RiseMix mix)
    {
        return _loaded.GetValueOrDefault(mix);
    }

    /// <summary>Loads one mix's list now — a bulk capture run starts from a fresh one. False when it could not.</summary>
    public async Task<bool> RefreshAsync(RiseMix mix, CancellationToken cancellationToken)
    {
        switch (await site.GetChartsAsync(mix, cancellationToken))
        {
            case SiteResult<IReadOnlyList<CatalogChart>>.Ok ok:
                var catalog = new SongCatalog(ok.Value);
                _loaded[mix] = catalog;
                log.LogInformation("Chart list for {Mix}: {Catalog}", mix, catalog);
                return true;
            case SiteResult<IReadOnlyList<CatalogChart>>.Failed failed:
                log.LogWarning("The chart list for {Mix} could not be loaded: {Status} {Message}", mix, failed.Status, failed.Message);
                return false;
            default:
                log.LogWarning("The chart list for {Mix} could not be loaded: the token was not accepted", mix);
                return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (status.Player is not null && !status.TokenRejected)
                    foreach (var mix in Mixes)
                        await RefreshAsync(mix, stoppingToken);
                var complete = Mixes.All(mix => _loaded.ContainsKey(mix));
                await Task.Delay(complete ? TimeSpan.FromHours(12) : TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
    }
}

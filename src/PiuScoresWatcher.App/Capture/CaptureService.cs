using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Runs the sources the settings ask for and feeds every frame through the pipeline, one at a
///     time. What each frame became goes to the log; the notifier has already told the player.
/// </summary>
public sealed class CaptureService(
    ISettingsStore settings,
    CapturePipeline pipeline,
    WindowCaptureSource window,
    Func<SteamScreenshotSource> steamScreenshots,
    ILogger<CaptureService> log) : BackgroundService
{
    private readonly SemaphoreSlim _oneFrameAtATime = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mode = settings.Load().Mode;
        var sources = new List<IScreenSource>();
        if (mode is CaptureMode.Game or CaptureMode.Both)
            sources.Add(window);
        if (mode is CaptureMode.SteamScreenshots or CaptureMode.Both)
            sources.Add(steamScreenshots());
        log.LogInformation("Capture mode {Mode}: {Sources}", mode, string.Join(", ", sources.Select(s => s.Kind)));

        await Task.WhenAll(sources.Select(source => source.RunAsync(frame => HandleAsync(frame, stoppingToken), stoppingToken)));
    }

    private async Task HandleAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        await _oneFrameAtATime.WaitAsync(cancellationToken);
        try
        {
            var outcome = await pipeline.HandleAsync(frame, cancellationToken);
            switch (outcome)
            {
                case FrameOutcome.Posted posted:
                    log.LogInformation("{Source}: {Song} {Type} {Level} {Score} → {Outcome}", frame.Source, posted.Play.SongName,
                        posted.Play.ChartType, posted.Play.Level, posted.Play.Score, posted.Outcome.Describe());
                    break;
                case FrameOutcome.Kept kept:
                    log.LogWarning("{Source}: kept for review — {Reason} ({Path})", frame.Source, kept.Reason, kept.SavedTo);
                    break;
                case FrameOutcome.NotAPlay:
                    log.LogInformation("{Source}: a result screen that is not one play (a Challenge aggregate); skipped", frame.Source);
                    break;
                case FrameOutcome.NotYet notYet:
                    log.LogDebug("{Source}: not yet — {Reason}", frame.Source, notYet.Reason);
                    break;
                case FrameOutcome.Duplicate:
                    log.LogDebug("{Source}: the same play again", frame.Source);
                    break;
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // one bad frame must not stop the watcher; the next frame is a second from now
            log.LogError(failure, "{Source}: a frame could not be handled", frame.Source);
        }
        finally
        {
            _oneFrameAtATime.Release();
        }
    }
}

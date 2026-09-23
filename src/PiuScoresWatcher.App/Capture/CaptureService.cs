using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Runs the sources the settings ask for and feeds every frame through the pipeline, one at a
///     time. Pausing, or changing the mode or the screenshots folder, ends the current round and starts
///     the next with what is asked for now. What each frame became goes to the log; the notifier has
///     already told the player.
/// </summary>
public sealed class CaptureService(
    ISettingsStore settings,
    WatcherStatus status,
    CapturePipeline pipeline,
    WindowCaptureSource window,
    Func<SteamScreenshotSource> steamScreenshots,
    ILogger<CaptureService> log) : BackgroundService
{
    private readonly SemaphoreSlim _oneFrameAtATime = new(1, 1);
    private CancellationTokenSource? _round;
    private (CaptureMode Mode, string? Folder)? _watching;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        settings.Changed += OnSettingsChanged;
        status.PausedChanged += OnPausedChanged;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var round = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                Volatile.Write(ref _round, round);
                try
                {
                    await RunRoundAsync(round.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // restarted: paused, resumed, or a new mode
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        finally
        {
            settings.Changed -= OnSettingsChanged;
            status.PausedChanged -= OnPausedChanged;
        }
    }

    private async Task RunRoundAsync(CancellationToken cancellationToken)
    {
        if (status.Paused)
        {
            log.LogInformation("Paused; watching nothing until resumed");
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return;
        }

        var current = settings.Load();
        _watching = (current.Mode, current.SteamScreenshotsFolder);
        var sources = new List<IScreenSource>();
        if (current.Mode is CaptureMode.Game or CaptureMode.Both)
            sources.Add(window);
        if (current.Mode is CaptureMode.SteamScreenshots or CaptureMode.Both)
            sources.Add(steamScreenshots());
        log.LogInformation("Capture mode {Mode}: {Sources}", current.Mode, string.Join(", ", sources.Select(s => s.Kind)));

        await Task.WhenAll(sources.Select(source => source.RunAsync(frame => HandleAsync(frame, cancellationToken), cancellationToken)));
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private void OnSettingsChanged(object? sender, WatcherSettings saved)
    {
        // a notification switch or the remembered version is no reason to restart the sources
        if (_watching != (saved.Mode, saved.SteamScreenshotsFolder))
            EndRound();
    }

    private void OnPausedChanged(object? sender, EventArgs e)
    {
        EndRound();
    }

    private void EndRound()
    {
        try
        {
            Volatile.Read(ref _round)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // the round had already ended; the next one reads the new state anyway
        }
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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Runs the sources the settings ask for and feeds every frame through the pipeline, one at a
///     time. Pausing, changing the mode or the screenshots folder, or a bulk capture starting or ending
///     ends the current round and starts the next with what is asked for now. While a bulk capture runs,
///     every frame goes to it first and the game window is watched whatever the mode (D51); a frame that
///     is not the song list still reaches the result pipeline, and a result screen ends the run. What each
///     frame became goes to the log; the notifier has already told the player.
///     A frame already being handled when a round ends is finished, not dropped: an F12 screenshot is read once, so
///     a post cut off by a pause would lose its play. A source that fails is started again after a wait, and so is a
///     round that fails — nothing short of quitting stops the watching.
/// </summary>
public sealed class CaptureService(
    ISettingsStore settings,
    WatcherStatus status,
    CapturePipeline pipeline,
    BulkCaptureService bulk,
    WindowCaptureSource window,
    Func<SteamScreenshotSource> steamScreenshots,
    ILogger<CaptureService> log) : BackgroundService
{
    /// <summary>The first wait before a failed source or round starts again; it doubles while the failures repeat.</summary>
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _oneFrameAtATime = new(1, 1);
    private CancellationTokenSource? _round;
    private (CaptureMode Mode, string? Folder)? _watching;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        settings.Changed += OnSettingsChanged;
        status.PausedChanged += OnRestart;
        bulk.RunningChanged += OnRestart;
        var retry = FirstRetry;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var round = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                Volatile.Write(ref _round, round);
                try
                {
                    await RunRoundAsync(round.Token, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // restarted: paused, resumed, a new mode, a bulk capture starting or ending
                    retry = FirstRetry;
                }
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    log.LogError(failure, "Capture could not start; trying again in {Wait}", retry);
                    await Task.Delay(retry, stoppingToken);
                    retry = Longer(retry);
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
            status.PausedChanged -= OnRestart;
            bulk.RunningChanged -= OnRestart;
        }
    }

    /// <summary>One round of watching; frames are handled on <paramref name="stoppingToken" />, so ending the round never cuts one off.</summary>
    private async Task RunRoundAsync(CancellationToken cancellationToken, CancellationToken stoppingToken)
    {
        var bulkRunning = bulk.IsRunning;
        if (status.Paused && !bulkRunning)
        {
            log.LogInformation("Paused; watching nothing until resumed");
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return;
        }

        var current = settings.Load();
        _watching = (current.Mode, current.SteamScreenshotsFolder);
        var sources = new List<IScreenSource>();
        if (current.Mode is CaptureMode.Game or CaptureMode.Both || bulkRunning)
            sources.Add(window);
        if (current.Mode is CaptureMode.SteamScreenshots or CaptureMode.Both)
            sources.Add(steamScreenshots());
        log.LogInformation("Capture mode {Mode}{Bulk}: {Sources}", current.Mode, bulkRunning ? " with a bulk capture" : "",
            string.Join(", ", sources.Select(s => s.Kind)));

        await Task.WhenAll(sources.Select(source => KeepRunningAsync(source, frame => HandleAsync(frame, stoppingToken), cancellationToken)));
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    /// <summary>One source for the round; one that fails is started again after a wait instead of ending the watching.</summary>
    private async Task KeepRunningAsync(IScreenSource source, Func<CapturedFrame, Task> onFrame, CancellationToken cancellationToken)
    {
        var retry = FirstRetry;
        while (true)
        {
            try
            {
                await source.RunAsync(onFrame, cancellationToken);
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                log.LogError(failure, "{Source} stopped; starting it again in {Wait}", source.Kind, retry);
                await Task.Delay(retry, cancellationToken);
                retry = Longer(retry);
            }
        }
    }

    private static TimeSpan Longer(TimeSpan retry)
    {
        return retry * 2 < LongestRetry ? retry * 2 : LongestRetry;
    }

    private void OnSettingsChanged(object? sender, WatcherSettings saved)
    {
        // a notification switch, the sounds or the remembered version is no reason to restart the sources
        if (_watching != (saved.Mode, saved.SteamScreenshotsFolder))
            EndRound();
    }

    private void OnRestart(object? sender, EventArgs e)
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
            var bulkOutcome = await bulk.HandleAsync(frame, cancellationToken);
            if (bulkOutcome is not null and not BulkOutcome.NotTheList)
                return; // Warm Up's song list: nothing for the result pipeline

            var outcome = await pipeline.HandleAsync(frame, cancellationToken);
            if (bulkOutcome is BulkOutcome.NotTheList && outcome is not FrameOutcome.NotAResult)
                bulk.Stop("a result screen appeared");
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

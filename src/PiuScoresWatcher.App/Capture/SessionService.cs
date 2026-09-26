using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Sessions;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Ends sessions the way the switches ask (D74–D82), with <see cref="SessionKeeper" /> holding what is open and owed:
///     when RISE closes, ten seconds on so a last F12 is posted first (D79); once the quiet minutes have run out (D78);
///     and with either switch on, around a bulk capture (D80) and when the watcher quits (D81). Every <see cref="Check" />
///     it looks at the minutes, at a session PIU Scores has ended by itself, and at a close still owed (D82). A close
///     takes its turn at the <see cref="PlayGate" />, and whether it is still due is decided once it has the turn, so a
///     play recorded while it waited keeps the session open.
/// </summary>
public sealed class SessionService(
    SessionKeeper keeper,
    ISettingsStore settings,
    IGameSession game,
    BulkCaptureService bulk,
    PlayGate gate,
    ILogger<SessionService> log) : BackgroundService
{
    /// <summary>How often the quiet minutes, the site's four hours and a close still owed are looked at.</summary>
    public static readonly TimeSpan Check = TimeSpan.FromSeconds(30);

    /// <summary>After RISE closes, long enough for Steam to finish writing an F12 of the last result and the watcher to post it (D79).</summary>
    public static readonly TimeSpan AfterRiseCloses = TimeSpan.FromSeconds(10);

    /// <summary>At start-up, time for the first look at the game's process before a session a crash left open is judged (D81).</summary>
    private static readonly TimeSpan FirstLook = TimeSpan.FromSeconds(5);

    /// <summary>At exit, how long the capture loop may keep its turn before the close goes without it.</summary>
    private static readonly TimeSpan TurnAtExit = TimeSpan.FromSeconds(1);

    private CancellationToken _stopping;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        game.RunningChanged += OnGameRunningChanged;
        bulk.RunningChanged += OnBulkRunningChanged;
        try
        {
            // closes the last run couldn't send go out before this run's first play can join their sessions
            await EndAsync("", _ => false, loud: true, stoppingToken);
            await Task.Delay(FirstLook, stoppingToken);
            await EndAsync("RISE isn't running at start-up", sessions => sessions.WhenRiseCloses && !game.IsRunning, loud: true, stoppingToken);
            while (true)
            {
                var minutes = settings.Load().EffectiveSessions.EffectiveQuietMinutes;
                await EndAsync($"{minutes} minutes without a play", sessions => sessions.AfterQuiet && keeper.QuietFor(sessions.Quiet), loud: false,
                    stoppingToken);
                await Task.Delay(Check, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // stopping
        }
        finally
        {
            game.RunningChanged -= OnGameRunningChanged;
            bulk.RunningChanged -= OnBulkRunningChanged;
        }
    }

    /// <summary>
    ///     The watcher is quitting, or Windows is ending: it can't follow the session any further, so with either switch on
    ///     it ends it now (D81). The close is written down first, and whatever can't reach PIU Scores within
    ///     <paramref name="within" /> goes out at the next start. Called on the UI thread as the app exits, so the work runs
    ///     off it.
    /// </summary>
    public void EndOnExit(TimeSpan within)
    {
        if (!keeper.IsOpen && !keeper.HasOwed)
            return;
        try
        {
            if (!Task.Run(() => EndAtExitAsync(within)).Wait(within + TurnAtExit))
                log.LogWarning("The session's close didn't finish as the watcher closed; it goes out at the next start");
        }
        catch (AggregateException failure)
        {
            log.LogWarning(failure.InnerException, "The session could not be ended as the watcher closed; its close goes out at the next start");
        }
    }

    private async Task EndAtExitAsync(TimeSpan within)
    {
        using var timeout = new CancellationTokenSource(within);
        // the capture loop may still hold its turn; this is the watcher's last word, so it doesn't wait long for it
        var turn = await gate.Turn.WaitAsync(TurnAtExit);
        try
        {
            if (keeper.IsOpen && settings.Load().EffectiveSessions.EndsSessions)
            {
                keeper.End();
                log.LogInformation("Session ended: the watcher is closing");
            }

            if (keeper.HasOwed)
                Report(await keeper.SendOwedAsync(timeout.Token), loud: true);
        }
        finally
        {
            if (turn)
                gate.Turn.Release();
        }
    }

    private void OnGameRunningChanged(object? sender, bool running)
    {
        if (running)
            return;
        Fire(async () =>
        {
            // an F12 of the last result may still be on its way (D79)
            await Task.Delay(AfterRiseCloses, _stopping);
            await EndAsync("RISE closed", sessions => sessions.WhenRiseCloses && !game.IsRunning, loud: true, _stopping);
        });
    }

    private void OnBulkRunningChanged(object? sender, EventArgs e)
    {
        var why = bulk.IsRunning ? "a bulk capture started" : "the bulk capture ended";
        Fire(() => EndAsync(why, sessions => sessions.EndsSessions, loud: true, _stopping));
    }

    /// <summary>
    ///     With the turn at the gate: lets a session PIU Scores has ended by itself lapse, ends the open one when
    ///     <paramref name="due" /> says so under the switches as they are now, and sends whatever close is owed.
    ///     <paramref name="loud" /> logs a close that didn't get through as a warning; a retry's is only a debug line.
    /// </summary>
    private async Task EndAsync(string why, Func<SessionSettings, bool> due, bool loud, CancellationToken cancellationToken)
    {
        if (!keeper.IsOpen && !keeper.HasOwed)
            return;
        await gate.Turn.WaitAsync(cancellationToken);
        try
        {
            if (keeper.Lapse())
                log.LogInformation("A session went four hours without a play; PIU Scores has ended it itself");
            var ended = keeper.IsOpen && due(settings.Load().EffectiveSessions);
            if (ended)
            {
                keeper.End();
                log.LogInformation("Session ended: {Why}", why);
            }

            if (keeper.HasOwed)
                Report(await keeper.SendOwedAsync(cancellationToken), loud || ended);
        }
        finally
        {
            gate.Turn.Release();
        }
    }

    private void Report(IReadOnlyList<SentClose> sent, bool loud)
    {
        foreach (var close in sent)
        {
            if (close.Outcome is null)
                log.LogInformation("{Mix}: a session's close was four hours old and was dropped; PIU Scores has ended it itself", close.Mix);
            else if (!close.Outcome.WorthRetrying)
                log.LogInformation("{Mix}: the session's close is done — {Outcome}", close.Mix, close.Outcome.Describe());
            else if (loud)
                log.LogWarning("{Mix}: the session's close didn't get through — {Outcome}; it goes out again", close.Mix, close.Outcome.Describe());
            else
                log.LogDebug("{Mix}: the session's close still didn't get through — {Outcome}", close.Mix, close.Outcome.Describe());
        }
    }

    /// <summary>An end set off by an event, off the thread that raised it; a failure is logged, never thrown at the raiser.</summary>
    private void Fire(Func<Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await work();
            }
            catch (OperationCanceledException)
            {
                // stopping
            }
            catch (Exception failure)
            {
                log.LogError(failure, "A session could not be ended");
            }
        });
    }
}

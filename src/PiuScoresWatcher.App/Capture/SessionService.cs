using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Sessions;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Capture;

/// <summary>
///     Ends sessions the way the switches ask (D74–D82), with <see cref="SessionKeeper" /> holding what is open and owed:
///     when RISE closes, ten seconds on so a last F12 is posted first (D79); once the quiet minutes have run out, except
///     during a bulk capture, whose end ends it (D78); and with either switch on, around a bulk capture (D80) and when the
///     watcher quits (D81). Every <see cref="Check" /> it looks again: a session still open ten seconds after RISE closed —
///     one a crash left, a late F12's — ends too, a session PIU Scores has ended by itself lapses, and a close still owed
///     is sent again, less often while it keeps failing (D82). A close takes its turn at the <see cref="PlayGate" />, and
///     whether there is anything to end is decided once it has the turn, so a play recorded while it waited counts; a send
///     gives up after <see cref="CloseTimeout" />, so a site that hangs can't hold the capture loop for long.
/// </summary>
public sealed class SessionService(
    SessionKeeper keeper,
    ISettingsStore settings,
    IGameSession game,
    BulkCaptureService bulk,
    PlayGate gate,
    IClock clock,
    ILogger<SessionService> log) : BackgroundService
{
    /// <summary>How often the switches, the site's four hours and a close still owed are looked at.</summary>
    public static readonly TimeSpan Check = TimeSpan.FromSeconds(30);

    /// <summary>After RISE closes, long enough for Steam to finish writing an F12 of the last result and the watcher to post it (D79).</summary>
    public static readonly TimeSpan AfterRiseCloses = TimeSpan.FromSeconds(10);

    /// <summary>How long one close may hold the capture loop's turn; a close that doesn't get through in time stays owed.</summary>
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(3);

    /// <summary>The longest wait between tries of a close that keeps failing.</summary>
    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(5);

    /// <summary>At start-up, time for the first look at the game's process before "RISE isn't running" can end a session a crash left open.</summary>
    private static readonly TimeSpan FirstLook = TimeSpan.FromSeconds(5);

    /// <summary>At exit, how long the capture loop may keep its turn; past that the close goes out at the next start instead (D81).</summary>
    private static readonly TimeSpan TurnAtExit = TimeSpan.FromSeconds(1.5);

    private CancellationToken _stopping;

    /// <summary>When RISE last closed, in UTC ticks; 0 until it has been seen closing, which reads as long ago.</summary>
    private long _riseClosedTicks;

    // read and written only with the turn at the gate
    private DateTimeOffset _nextRetry = DateTimeOffset.MinValue;
    private TimeSpan _retryWait = Check;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        game.RunningChanged += OnGameRunningChanged;
        bulk.RunningChanged += OnBulkRunningChanged;
        try
        {
            // closes the last run couldn't send go out before this run's first play can join their sessions
            await SafelyAsync(() => EndAsync(_ => null, loud: true, stoppingToken));
            await Task.Delay(FirstLook, stoppingToken);
            while (true)
            {
                await SafelyAsync(() => EndAsync(Due, loud: false, stoppingToken));
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
            if (!Task.Run(() => EndAtExitAsync(within)).Wait(TurnAtExit + within))
                log.LogWarning("The session's close didn't finish as the watcher closed; it goes out at the next start");
        }
        catch (AggregateException failure)
        {
            log.LogWarning(failure.InnerException, "The session could not be ended as the watcher closed; its close goes out at the next start");
        }
    }

    private async Task EndAtExitAsync(TimeSpan within)
    {
        var ending = settings.Load().EffectiveSessions.EndsSessions;
        // the capture loop may still be posting the session's last play: the close goes after it, or at the next start
        if (!await gate.Turn.WaitAsync(TurnAtExit))
        {
            if (ending && keeper.IsOpen)
            {
                keeper.End();
                log.LogInformation("Session ended: the watcher is closing; its close goes out at the next start");
            }

            return;
        }

        try
        {
            if (ending && keeper.IsOpen)
            {
                keeper.End();
                log.LogInformation("Session ended: the watcher is closing");
            }

            if (keeper.HasOwed)
            {
                using var timeout = new CancellationTokenSource(within);
                Report(await keeper.SendOwedAsync(timeout.Token), loud: true);
            }
        }
        finally
        {
            gate.Turn.Release();
        }
    }

    private void OnGameRunningChanged(object? sender, bool running)
    {
        if (running)
            return;
        Volatile.Write(ref _riseClosedTicks, clock.Now.UtcTicks);
        Fire(async () =>
        {
            // an F12 of the last result may still be on its way (D79)
            await Task.Delay(AfterRiseCloses, _stopping);
            await EndAsync(sessions => sessions.WhenRiseCloses && !game.IsRunning ? "RISE closed" : null, loud: true, _stopping);
        });
    }

    private void OnBulkRunningChanged(object? sender, EventArgs e)
    {
        var why = bulk.IsRunning ? "a bulk capture started" : "the bulk capture ended";
        Fire(() => EndAsync(sessions => sessions.EndsSessions ? why : null, loud: true, _stopping));
    }

    /// <summary>
    ///     The look every <see cref="Check" />: why the open session is due to end under the switches as they are, or null.
    ///     RISE not running is a standing reason, so a session the RISE-closed look missed — a crash's, a late F12's — ends
    ///     too; the quiet minutes don't run during a bulk capture, whose end ends the session (D80).
    /// </summary>
    private string? Due(SessionSettings sessions)
    {
        if (sessions.WhenRiseCloses && !game.IsRunning && clock.Now.UtcTicks - Volatile.Read(ref _riseClosedTicks) >= AfterRiseCloses.Ticks)
            return "RISE isn't running";
        if (sessions.AfterQuiet && !bulk.IsRunning && keeper.QuietFor(sessions.Quiet))
            return $"{sessions.EffectiveQuietMinutes} minutes without a play";
        return null;
    }

    /// <summary>
    ///     With the turn at the gate: lets a session PIU Scores has ended by itself lapse, ends the open one when
    ///     <paramref name="due" /> gives a reason under the switches as they are now, and sends whatever close is owed —
    ///     straight away after an end, and on the retry's clock otherwise. <paramref name="loud" /> logs a close that didn't
    ///     get through as a warning; a retry's is only a debug line.
    /// </summary>
    private async Task EndAsync(Func<SessionSettings, string?> due, bool loud, CancellationToken cancellationToken)
    {
        await gate.Turn.WaitAsync(cancellationToken);
        try
        {
            if (keeper.Lapse())
                log.LogInformation("A session went four hours without a play; PIU Scores has ended it itself");
            var ended = false;
            if (keeper.IsOpen && due(settings.Load().EffectiveSessions) is { } why)
            {
                keeper.End();
                ended = true;
                log.LogInformation("Session ended: {Why}", why);
            }

            if (keeper.HasOwed && (loud || ended || clock.Now >= _nextRetry))
                await SendAsync(loud || ended, cancellationToken);
        }
        finally
        {
            gate.Turn.Release();
        }
    }

    /// <summary>The closes owed, each cut off after <see cref="CloseTimeout" />; while one keeps failing, the next try waits twice as long, up to five minutes.</summary>
    private async Task SendAsync(bool loud, CancellationToken cancellationToken)
    {
        Report(await keeper.SendOwedAsync(CloseTimeout, cancellationToken), loud);
        _retryWait = !keeper.HasOwed ? Check : _retryWait * 2 < LongestRetry ? _retryWait * 2 : LongestRetry;
        _nextRetry = clock.Now + _retryWait;
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

    /// <summary>One look at the sessions; a failure is logged and the next look comes as usual, so ending sessions never stops for good.</summary>
    private async Task SafelyAsync(Func<Task> look)
    {
        try
        {
            await look();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            log.LogError(failure, "Sessions could not be looked at; looking again in {Wait}", Check);
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

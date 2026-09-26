using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.Core.Sessions;

/// <summary>One close the keeper dealt with: the mix, and what PIU Scores answered, or null when it was dropped unsent (D82).</summary>
[ExcludeFromCodeCoverage]
public sealed record SentClose(RiseMix Mix, CloseOutcome? Outcome);

/// <summary>
///     The watcher's half of a session (D74–D82). PIU Scores keeps a play in the open session on its mix for up to four
///     hours, and closes a session when told to or by itself after four hours without a play; when to tell it is the
///     watcher's. The keeper knows the mixes the watcher has posted to since it last ended a session (a session is all of
///     them, D77) and ends it with one close per mix. A close is written down before it is sent, so a watcher that dies
///     mid-request still sends it at its next start; one that didn't get through is sent again until it lands, and dropped
///     unsent four hours after its session's last play, when the site has ended that session itself and a late close could
///     only end a newer one. When to end is the App's call: RISE closing, the quiet minutes, a bulk capture, quitting.
/// </summary>
public sealed class SessionKeeper
{
    /// <summary>How long after a session's last play PIU Scores ends it by itself (D76; rise.md D21 as revised for RISE, 2026-09-26).</summary>
    public static readonly TimeSpan SiteFallback = TimeSpan.FromHours(4);

    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly IPlaysClient _site;
    private readonly ISessionStore _store;
    private SessionState _state;

    public SessionKeeper(IPlaysClient site, ISessionStore store, IClock clock)
    {
        _site = site;
        _store = store;
        _clock = clock;
        _state = store.Load();
    }

    /// <summary>A play was recorded since the last end: there is a session to end.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _state.Open.Count > 0;
        }
    }

    /// <summary>A close was decided on and hasn't reached PIU Scores yet.</summary>
    public bool HasOwed
    {
        get
        {
            lock (_gate)
                return _state.Owed.Count > 0;
        }
    }

    /// <summary>When the open session last had a play, on either station; null when none is open.</summary>
    public DateTimeOffset? LastPlayAt
    {
        get
        {
            lock (_gate)
                return _state.Open.Count == 0 ? null : _state.Open.Max(open => open.LastPlayAt);
        }
    }

    /// <summary>
    ///     PIU Scores recorded a play on <paramref name="mix" />: its session is open, and the quiet starts again. A close
    ///     still owed on the mix is moot: the play joined the session it was for, and that session is the open one again.
    /// </summary>
    public void Recorded(RiseMix mix)
    {
        lock (_gate)
            Change(new SessionState(
                [.. _state.Open.Where(open => open.Mix != mix), new MixSession(mix, _clock.Now)],
                [.. _state.Owed.Where(owed => owed.Mix != mix)]));
    }

    /// <summary>The open session has had no play for <paramref name="quiet" />.</summary>
    public bool QuietFor(TimeSpan quiet)
    {
        return LastPlayAt is { } last && _clock.Now - last >= quiet;
    }

    /// <summary>
    ///     Ends the open session: every mix posted to since the last end is owed a close, written down now. Nothing is sent
    ///     until <see cref="SendOwedAsync" />.
    /// </summary>
    public void End()
    {
        lock (_gate)
        {
            if (_state.Open.Count == 0)
                return;
            Change(new SessionState([], [.. _state.Owed.Where(owed => _state.Open.All(open => open.Mix != owed.Mix)), .. _state.Open]));
        }
    }

    /// <summary>
    ///     Sends every close owed, one per mix. One PIU Scores took, or answered finally, is done with; one that didn't get
    ///     through stays owed for the next try. One owed four hours after its session's last play is dropped unsent.
    /// </summary>
    public Task<IReadOnlyList<SentClose>> SendOwedAsync(CancellationToken cancellationToken)
    {
        return SendOwedAsync(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    /// <summary>
    ///     <see cref="SendOwedAsync(CancellationToken)" />, each close given up on after <paramref name="eachWithin" /> and
    ///     left owed, so one that hangs doesn't cost the other its turn.
    /// </summary>
    public async Task<IReadOnlyList<SentClose>> SendOwedAsync(TimeSpan eachWithin, CancellationToken cancellationToken)
    {
        IReadOnlyList<MixSession> owed;
        lock (_gate)
            owed = _state.Owed;

        var sent = new List<SentClose>();
        foreach (var close in owed)
        {
            CloseOutcome? outcome = null;
            // a close four hours after its session's last play is dropped: the site has ended that session itself, and a
            // close now could only end a newer one
            if (_clock.Now - close.LastPlayAt < SiteFallback)
            {
                using var one = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                one.CancelAfter(eachWithin);
                outcome = await _site.CloseSittingsAsync(close.Mix, one.Token);
            }

            sent.Add(new SentClose(close.Mix, outcome));
            if (outcome is { WorthRetrying: true })
                continue;
            lock (_gate)
                Change(_state with { Owed = [.. _state.Owed.Where(still => still != close)] });
        }

        return sent;
    }

    /// <summary>
    ///     Four hours after a session's last play PIU Scores has ended it by itself (D76), so the watcher stops counting it
    ///     as open: ending it later would close whatever is open on its mix by then. True when one lapsed.
    /// </summary>
    public bool Lapse()
    {
        lock (_gate)
        {
            var now = _clock.Now;
            var live = _state.Open.Where(open => now - open.LastPlayAt < SiteFallback).ToList();
            if (live.Count == _state.Open.Count)
                return false;
            Change(_state with { Open = live });
            return true;
        }
    }

    /// <summary>The token left: what was open or owed belonged to it (D82).</summary>
    public void Forget()
    {
        lock (_gate)
            Change(SessionState.Empty);
    }

    private void Change(SessionState state)
    {
        _state = state;
        _store.Save(state);
    }
}

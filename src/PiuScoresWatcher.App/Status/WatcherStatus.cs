using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Status;

/// <summary>One line of the settings window's Recent list.</summary>
public sealed record RecentPlay(ObservedPlay Play, bool Recorded, DateTimeOffset At);

/// <summary>
///     What the watcher is doing, for the tray's status line and the settings window: who the token
///     is, whether RISE is running, whether watching is paused, the plays since it started. The
///     notifier, the capture loop and the windows write it; <see cref="Changed" /> fires on any thread.
///     Nothing here is persisted — pause and the recent list start fresh with each launch (D42).
/// </summary>
public sealed class WatcherStatus
{
    private const int RecentLimit = 50;

    private readonly IClock _clock;
    private readonly IGameSession _game;
    private readonly object _gate = new();
    private readonly List<RecentPlay> _recent = [];
    private bool _hasToken;
    private PlayerIdentity? _player;
    private bool _tokenRejected;
    private bool _paused;
    private bool? _upToDate;
    private DateOnly _today;
    private int _recordedToday;
    private DateTimeOffset? _lastRecorded;

    public WatcherStatus(IClock clock, IGameSession game)
    {
        _clock = clock;
        _game = game;
        _game.RunningChanged += (_, _) => Raise();
    }

    /// <summary>Anything the tray or a window shows changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Only pausing or resuming — what the capture loop restarts for.</summary>
    public event EventHandler? PausedChanged;

    /// <summary>A token is stored. Its owner is <see cref="Player" /> once PIU Scores has said who it is.</summary>
    public bool HasToken { get { lock (_gate) return _hasToken; } }

    public PlayerIdentity? Player { get { lock (_gate) return _player; } }

    public bool TokenRejected { get { lock (_gate) return _tokenRejected; } }

    public bool Paused { get { lock (_gate) return _paused; } }

    public bool GameRunning => _game.IsRunning;

    /// <summary>True when the update check found nothing newer, false when an update waits for the next start, null before the check.</summary>
    public bool? UpToDate { get { lock (_gate) return _upToDate; } }

    public DateTimeOffset? LastRecorded { get { lock (_gate) return _lastRecorded; } }

    public int PlaysToday
    {
        get
        {
            lock (_gate)
                return _today == DateOnly.FromDateTime(_clock.Now.LocalDateTime) ? _recordedToday : 0;
        }
    }

    public IReadOnlyList<RecentPlay> Recent { get { lock (_gate) return _recent.ToList(); } }

    /// <summary>The one line the tray menu leads with.</summary>
    public string Headline =>
        !HasToken || TokenRejected ? Copy.StatusNotConnected
        : Paused ? Copy.StatusPaused
        : GameRunning ? Copy.StatusWatching
        : Copy.StatusWaiting;

    /// <summary>A token is stored; who it belongs to is not known yet (the site has not answered, or cannot be reached).</summary>
    public void TokenStored()
    {
        lock (_gate)
            _hasToken = true;
        Raise();
    }

    public void Connected(PlayerIdentity player)
    {
        lock (_gate)
        {
            _hasToken = true;
            _player = player;
            _tokenRejected = false;
        }

        Raise();
    }

    public void Disconnected()
    {
        lock (_gate)
        {
            _hasToken = false;
            _player = null;
            _tokenRejected = false;
        }

        Raise();
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_paused == paused)
                return;
            _paused = paused;
        }

        PausedChanged?.Invoke(this, EventArgs.Empty);
        Raise();
    }

    public void SetUpToDate(bool? upToDate)
    {
        lock (_gate)
            _upToDate = upToDate;
        Raise();
    }

    /// <summary>Folds a notice into the status; the notifier calls it for every notice, shown or not.</summary>
    public void Record(WatcherNotice notice)
    {
        lock (_gate)
        {
            switch (notice)
            {
                case WatcherNotice.Recorded recorded:
                    Add(new RecentPlay(recorded.Play, true, _clock.Now));
                    _tokenRejected = false;
                    _lastRecorded = _clock.Now;
                    var today = DateOnly.FromDateTime(_clock.Now.LocalDateTime);
                    _recordedToday = _today == today ? _recordedToday + 1 : 1;
                    _today = today;
                    break;
                case WatcherNotice.NotRecorded notRecorded:
                    Add(new RecentPlay(notRecorded.Play, false, _clock.Now));
                    if (notRecorded.Outcome is PostOutcome.Unauthorized)
                        _tokenRejected = true;
                    break;
                case WatcherNotice.TokenRejected:
                    _tokenRejected = true;
                    break;
            }
        }

        Raise();
    }

    private void Add(RecentPlay play)
    {
        _recent.Insert(0, play);
        if (_recent.Count > RecentLimit)
            _recent.RemoveAt(_recent.Count - 1);
    }

    private void Raise()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

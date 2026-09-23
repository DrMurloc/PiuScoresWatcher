using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.Core.Capture;

/// <summary>The numbers that identify a play — never the title, which OCR may spell differently from one frame to the next.</summary>
[ExcludeFromCodeCoverage]
public sealed record PlayKey(RiseMix Mix, ChartType ChartType, int Level, Judgments Judgments, int MaxCombo, int Score, bool IsBroken)
{
    /// <summary>Only a complete reading has a key.</summary>
    public static PlayKey? Of(ResultScreenReading reading)
    {
        return reading.ChartType is { } type && reading.Level is { } level && reading.Judgments is { } judgments
               && reading.MaxCombo is { } combo && reading.Score is { } score
            ? new PlayKey(reading.Mix, type, level, judgments, combo, score, reading.IsBroken)
            : null;
    }
}

/// <summary>
///     A result screen stays up as long as the player leaves it, both sources may see it, and the
///     window source sees it once a second — so a play already handled inside the window is a
///     repeat. Handled means posted or kept for review; a frame that was only "not yet" is never
///     remembered, so the next one gets its turn.
/// </summary>
public sealed class Deduplicator(IClock clock)
{
    /// <summary>Long enough to outlast any result screen; short enough that a genuine identical repeat later still counts.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly Dictionary<PlayKey, DateTimeOffset> _handled = [];

    public bool IsNew(PlayKey key)
    {
        Expire();
        return !_handled.ContainsKey(key);
    }

    public void Remember(PlayKey key)
    {
        _handled[key] = clock.Now;
    }

    private void Expire()
    {
        var now = clock.Now;
        foreach (var stale in _handled.Where(pair => now - pair.Value > Window).Select(pair => pair.Key).ToList())
            _handled.Remove(stale);
    }
}

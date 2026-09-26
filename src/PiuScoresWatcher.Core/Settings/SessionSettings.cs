using System.Text.Json.Serialization;

namespace PiuScoresWatcher.Core.Settings;

/// <summary>
///     When the watcher ends a session, so PIU Scores posts its card (D75): when RISE closes (on by default), after some
///     minutes without a play (off by default), both, whichever comes first, or neither, when only PIU Scores' own four
///     hours without a play do (D76).
/// </summary>
/// <param name="WhenRiseCloses">End the session when RISE closes.</param>
/// <param name="AfterQuiet">End the session once <paramref name="QuietMinutes" /> pass without a play, RISE open or not (D78).</param>
/// <param name="QuietMinutes">One of <see cref="MinuteChoices" />; any other number reads as the default.</param>
public sealed record SessionSettings(bool WhenRiseCloses = true, bool AfterQuiet = false, int QuietMinutes = 15)
{
    public static SessionSettings Default { get; } = new();

    /// <summary>The minutes the switch offers (D78).</summary>
    public static IReadOnlyList<int> MinuteChoices { get; } = [10, 15, 20, 30, 45, 60];

    /// <summary>Either switch is on: the watcher ends sessions itself, around a bulk capture and on quitting too (D80, D81).</summary>
    [JsonIgnore]
    public bool EndsSessions => WhenRiseCloses || AfterQuiet;

    /// <summary>The minutes as the switch offers them: a number from a hand-edited file that isn't a choice reads as the default.</summary>
    [JsonIgnore]
    public int EffectiveQuietMinutes => MinuteChoices.Contains(QuietMinutes) ? QuietMinutes : Default.QuietMinutes;

    [JsonIgnore]
    public TimeSpan Quiet => TimeSpan.FromMinutes(EffectiveQuietMinutes);
}

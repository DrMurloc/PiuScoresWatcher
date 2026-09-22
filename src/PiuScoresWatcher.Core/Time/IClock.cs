namespace PiuScoresWatcher.Core.Time;

/// <summary>
///     The one door to the wall clock. A play's <c>playedAt</c>, a dedupe window and a log line all
///     read it, so a test can pin the time; nothing outside the App's <c>SystemClock</c> reads the
///     wall clock directly (arch-test enforced, ClockSeamTests — which scans comments too).
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

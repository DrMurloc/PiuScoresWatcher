using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.App.Time;

/// <summary>The one place the wall clock is read (arch-test enforced, ClockSeamTests).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

using PiuScoresWatcher.Core.Time;

namespace PiuScoresWatcher.Tests.TestHelpers;

/// <summary>A clock a test sets and moves.</summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; private set; } = now;

    public static FakeClock At(DateTimeOffset now) => new(now);

    public void Advance(TimeSpan by) => Now += by;
}

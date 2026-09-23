using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.CaptureTests;

public sealed class DeduplicatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 20, 13, 28, TimeSpan.FromHours(-4));

    private static PlayKey Key(int score = 945403)
    {
        return new PlayKey(RiseMix.Rise, ChartType.Single, 20, Judgments.From(911, 59, 13, 3, 14), 170, score, false);
    }

    [Fact]
    public void APlayIsNewUntilItIsRemembered()
    {
        var dedupe = new Deduplicator(FakeClock.At(Now));

        Assert.True(dedupe.IsNew(Key()));
        Assert.True(dedupe.IsNew(Key()));
        dedupe.Remember(Key());
        Assert.False(dedupe.IsNew(Key()));
    }

    [Fact]
    public void ADifferentScoreIsADifferentPlay()
    {
        var dedupe = new Deduplicator(FakeClock.At(Now));
        dedupe.Remember(Key(945403));

        Assert.True(dedupe.IsNew(Key(945404)));
    }

    [Fact]
    public void ARememberedPlayIsNewAgainOnceTheWindowHasPassed()
    {
        var clock = FakeClock.At(Now);
        var dedupe = new Deduplicator(clock);
        dedupe.Remember(Key());

        clock.Advance(Deduplicator.Window + TimeSpan.FromSeconds(1));

        Assert.True(dedupe.IsNew(Key()));
    }
}

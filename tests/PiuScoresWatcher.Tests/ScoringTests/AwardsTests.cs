using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Tests.ScoringTests;

/// <summary>The marks and plates on the owner's result screens, and the rules either side of them.</summary>
public sealed class AwardsTests
{
    private static Award? Award(RiseMix mix, int p, int g, int gd, int b, int m, bool broken = false)
    {
        return Awards.Of(mix, Judgments.From(p, g, gd, b, m), broken);
    }

    [Fact]
    public void RiseModeHandsOutItsThreeMarks()
    {
        Assert.Equal(Core.Scoring.Award.PerfectGame, Award(RiseMix.Rise, 71, 0, 0, 0, 0));
        Assert.Equal(Core.Scoring.Award.UltimateGame, Award(RiseMix.Rise, 833, 19, 0, 0, 0));   // Cynical: FULL COMBO
        Assert.Equal(Core.Scoring.Award.SuperbGame, Award(RiseMix.Rise, 1263, 16, 1, 0, 0));    // Nakakapagpabagabag: NO MISS
        Assert.Equal(Core.Scoring.Award.SuperbGame, Award(RiseMix.Rise, 100, 3, 0, 2, 0));      // bads, no misses: still NO MISS
    }

    [Fact]
    public void RiseModeShowsNoMarkOnceAMissIsIn()
    {
        Assert.Null(Award(RiseMix.Rise, 911, 59, 13, 3, 14));
        Assert.Null(Award(RiseMix.Rise, 999, 0, 0, 0, 1));
    }

    [Fact]
    public void TheArcadeStationHandsOutTheEightPlates()
    {
        Assert.Equal(Core.Scoring.Award.PerfectGame, Award(RiseMix.RiseArcade, 71, 0, 0, 0, 0));    // Ugly Dee D17
        Assert.Equal(Core.Scoring.Award.FairGame, Award(RiseMix.RiseArcade, 990, 0, 0, 0, 11));     // Ugly Dee D18
        Assert.Equal(Core.Scoring.Award.FairGame, Award(RiseMix.RiseArcade, 1038, 36, 9, 4, 14));   // Curiosity Overdrive S20
        Assert.Equal(Core.Scoring.Award.RoughGame, Award(RiseMix.RiseArcade, 946, 110, 14, 4, 26)); // 4NT S22
        Assert.Equal(Core.Scoring.Award.ExtremeGame, Award(RiseMix.RiseArcade, 100, 3, 2, 0, 0));
        Assert.Equal(Core.Scoring.Award.MarvelousGame, Award(RiseMix.RiseArcade, 100, 0, 0, 0, 5));
        Assert.Equal(Core.Scoring.Award.TalentedGame, Award(RiseMix.RiseArcade, 100, 0, 0, 0, 10));
        Assert.Equal(Core.Scoring.Award.FairGame, Award(RiseMix.RiseArcade, 100, 0, 0, 0, 20));
        Assert.Equal(Core.Scoring.Award.RoughGame, Award(RiseMix.RiseArcade, 100, 0, 0, 0, 21));
    }

    [Fact]
    public void ABrokenPlayCarriesNoAward()
    {
        Assert.Null(Award(RiseMix.Rise, 833, 19, 0, 0, 0, broken: true));
        Assert.Null(Award(RiseMix.RiseArcade, 71, 0, 0, 0, 0, broken: true));
    }
}

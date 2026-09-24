using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Tests.ScoringTests;

/// <summary>The grades on the owner's result screens, and the floors either side of them.</summary>
public sealed class GradesTests
{
    [Theory]
    [InlineData(469066, "F")]   // 1948 S26, the F that moved the site's D floor to 500,000
    [InlineData(563324, "C")]
    [InlineData(608610, "C")]
    [InlineData(678406, "B")]   // KUGUTSU S25
    [InlineData(777366, "A")]   // Neo Catharsis S25
    [InlineData(945403, "AA")]  // Morrighan S20
    [InlineData(968683, "S")]   // Curiosity Overdrive S20
    [InlineData(975429, "SS")]  // 86 S20
    [InlineData(991124, "SSS")] // Cynical S16
    [InlineData(500000, "D")]
    [InlineData(499999, "F")]
    [InlineData(1000000, "SSS")]
    public void RiseModeGradesOnItsNineGradeLadder(int score, string grade)
    {
        Assert.Equal(grade, Grades.Of(RiseMix.Rise, score));
    }

    [Theory]
    [InlineData(919853, "A+")]    // 4NT S22 — A+ exists only on Phoenix 2's floors
    [InlineData(960836, "AAA+")]  // Curiosity Overdrive S20
    [InlineData(988166, "SS+")]   // Ugly Dee D18
    [InlineData(1000000, "SSS+")] // Ugly Dee D17, a Perfect Game
    [InlineData(920000, "AA")]
    [InlineData(919999, "A+")]
    [InlineData(499999, "F")]
    public void TheArcadeStationGradesOnPhoenix2sFloors(int score, string grade)
    {
        Assert.Equal(grade, Grades.Of(RiseMix.RiseArcade, score));
    }
}

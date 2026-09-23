using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Tests.ScoringTests;

/// <summary>The owner's result screens, to the point: Warm Up, a half-double, the Arcade Station, tanked runs.</summary>
public sealed class PhoenixScoringTests
{
    [Theory]
    [InlineData(911, 59, 13, 3, 14, 170, 945403, "94.93")]      // Morrighan S20
    [InlineData(1263, 16, 1, 0, 0, 1279, 994399, "99.43")]     // Nakakapagpabagabag S18, NO MISS
    [InlineData(889, 68, 6, 6, 10, 263, 948168, "95.15")]      // Love Code S19
    [InlineData(724, 22, 2, 3, 38, 231, 932022, "93.52")]      // Curiosity Overdrive HD16, grey
    [InlineData(946, 110, 14, 4, 26, 343, 919853, "92.29")]    // 4NT S22, Arcade Station
    [InlineData(990, 0, 0, 0, 11, 821, 988166, "98.90")]       // Ugly Dee D18, Arcade Station
    [InlineData(71, 0, 0, 0, 0, 71, 1000000, "100.00")]        // Ugly Dee D17, PERFECT GAME
    [InlineData(637, 333, 216, 156, 558, 21, 469066, "47.13")] // 1948 S26, F
    [InlineData(877, 309, 206, 176, 77, 77, 678406, "68.15")]  // KUGUTSU S25, B
    public void TheScreenScoreFollowsFromTheJudgmentsAndMaxCombo(int p, int g, int gd, int b, int m, int combo, int score, string accuracy)
    {
        var judgments = Judgments.From(p, g, gd, b, m);

        Assert.Equal(score, PhoenixScoring.Score(judgments, combo));
        Assert.Equal(accuracy, PhoenixScoring.AccuracyShown(judgments));
    }

    [Fact]
    public void AccuracyIsTruncatedNeverRounded()
    {
        // 1272.8 / 1280 = 0.994375 -> 99.43, not 99.44
        Assert.Equal("99.43", PhoenixScoring.AccuracyShown(Judgments.From(1263, 16, 1, 0, 0)));
        Assert.Equal("9943", PhoenixScoring.AccuracyDigits(Judgments.From(1263, 16, 1, 0, 0)));
    }

    [Fact]
    public void MaxComboCannotExceedTheNotes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhoenixScoring.Score(Judgments.From(10, 0, 0, 0, 0), 11));
    }
}

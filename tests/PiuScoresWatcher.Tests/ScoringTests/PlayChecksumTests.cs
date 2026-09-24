using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Tests.ScoringTests;

public sealed class PlayChecksumTests
{
    private static ResultScreenReading Reading(int score, string accuracyDigits, ReadingStatus status = ReadingStatus.Complete)
    {
        return new ResultScreenReading(status, null, ResultLayout.DanceGrade, RiseMix.Rise, ChartType.Single, 20,
            Judgments.From(911, 59, 13, 3, 14), 170, score, accuracyDigits, false, new PixelRect(0, 0, 1, 1), 0.9);
    }

    [Fact]
    public void AReadingWhoseNumbersAgreeReconciles()
    {
        var verdict = PlayChecksum.Verify(Reading(945403, "9493"));

        Assert.True(verdict.Reconciles);
        Assert.Equal(945403, verdict.ExpectedScore);
        Assert.Equal("94.93", verdict.ExpectedAccuracy);
    }

    [Fact]
    public void AMisreadScoreIsRefused()
    {
        var verdict = PlayChecksum.Verify(Reading(945408, "9493"));

        Assert.False(verdict.Reconciles);
        Assert.Contains("945403", verdict.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AScoreOnePointFromTheFormulaReconcilesAndTwoDoNot()
    {
        // D57: the game's arithmetic lands a point off the integer formula now and then; PIU Scores allows it too
        Assert.True(PlayChecksum.Verify(Reading(945404, "9493")).Reconciles);
        Assert.True(PlayChecksum.Verify(Reading(945402, "9493")).Reconciles);
        Assert.False(PlayChecksum.Verify(Reading(945405, "9493")).Reconciles);
    }

    [Fact]
    public void AnAccuracyThatDoesNotBeginWithTheJudgmentsDigitsIsRefused()
    {
        Assert.False(PlayChecksum.Verify(Reading(945403, "9498")).Reconciles);
    }

    [Fact]
    public void AnAccuracyOneHundredthUnderTheExactValueStillReconciles()
    {
        // the game truncates a float: 1452.6 / 1500 is exactly 96.84 and PARADOXX's screen prints 96.83
        Assert.True(PlayChecksum.Verify(Reading(945403, "9492")).Reconciles);
        Assert.False(PlayChecksum.Verify(Reading(945403, "9491")).Reconciles);
    }

    [Fact]
    public void ASplitPercentSignAfterTheRightDigitsStillReconciles()
    {
        Assert.True(PlayChecksum.Verify(Reading(945403, "949396")).Reconciles);
    }

    [Fact]
    public void AnIncompleteReadingNeverReconciles()
    {
        Assert.False(PlayChecksum.Verify(Reading(945403, "9493", ReadingStatus.NumbersNotShown)).Reconciles);
    }
}

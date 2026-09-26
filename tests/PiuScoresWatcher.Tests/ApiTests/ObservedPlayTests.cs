using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Exceptions;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Tests.ApiTests;

public sealed class ObservedPlayTests
{
    private static readonly DateTimeOffset When = new(2026, 9, 22, 20, 13, 28, TimeSpan.FromHours(-4));

    private static ResultScreenReading Reading(ReadingStatus status = ReadingStatus.Complete)
    {
        return new ResultScreenReading(status, null, ResultLayout.Arcade, RiseMix.RiseArcade, ChartType.Double, 18,
            Judgments.From(990, 0, 0, 0, 11), 821, 988166, "9890", false, new PixelRect(0, 0, 1, 1), 0.9);
    }

    [Fact]
    public void ACompleteReadingWithItsTitleIsAPlay()
    {
        var play = ObservedPlay.From(Reading(), "  Ugly Dee ", When);

        Assert.Equal(RiseMix.RiseArcade, play.Mix);
        Assert.Equal("Ugly Dee", play.SongName);
        Assert.Equal(ChartType.Double, play.ChartType);
        Assert.Equal(18, play.Level);
        Assert.Equal(988166, play.Score);
        Assert.Equal(When, play.PlayedAt);
    }

    [Fact]
    public void AnIncompleteReadingIsRefused()
    {
        Assert.Throws<IncompletePlayException>(() => ObservedPlay.From(Reading(ReadingStatus.NumbersNotShown), "Ugly Dee", When));
    }

    [Fact]
    public void AMissingTitleIsRefused()
    {
        Assert.Throws<IncompletePlayException>(() => ObservedPlay.From(Reading(), "   ", When));
    }

    [Fact]
    public void APerfectGameIsEveryJudgmentAPerfectOrACaptureOfAMillion()
    {
        Assert.True(ObservedPlay.From(Reading() with { Judgments = Judgments.From(1001, 0, 0, 0, 0), Score = 1_000_000 }, "Ugly Dee", When).IsPerfectGame);
        Assert.False(ObservedPlay.From(Reading(), "Ugly Dee", When).IsPerfectGame);
        Assert.True(ObservedPlay.Captured(RiseMix.Rise, "B2", ChartType.Single, 7, 1_000_000, When).IsPerfectGame);
        Assert.False(ObservedPlay.Captured(RiseMix.Rise, "Aragami", ChartType.Single, 19, 999_999, When).IsPerfectGame);
    }

    [Fact]
    public void AGreyGradeIsNoPerfectGame()
    {
        var reading = Reading() with { Mix = RiseMix.Rise, Judgments = Judgments.From(1001, 0, 0, 0, 0), Score = 1_000_000, IsBroken = true };

        Assert.False(ObservedPlay.From(reading, "Ugly Dee", When).IsPerfectGame);
    }

    [Theory]
    [InlineData(CaptureSource.Replay, "watcher-replay")]
    [InlineData(CaptureSource.GameWindow, "watcher-grab")]
    [InlineData(CaptureSource.SteamScreenshot, "watcher-f12")]
    public void EverySourceTokenFitsTheApisThirtyTwoCharacters(CaptureSource source, string token)
    {
        Assert.Equal(token, source.Token());
        Assert.InRange(token.Length, 1, 32);
    }
}

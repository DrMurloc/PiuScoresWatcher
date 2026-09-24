using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.RecognitionTests;

/// <summary>
///     Every fixture screen in, the numbers on it out. The checksum runs on each complete reading as
///     well: a screen that reads but does not reconcile is a misread by definition.
/// </summary>
public sealed class ResultScreenReaderTests
{
    private readonly ResultScreenDetector _detector = new();
    private readonly ResultScreenReader _reader = new();

    public static TheoryData<string> Results()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("result"));
    }

    public static TheoryData<string> Empties()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("empty"));
    }

    public static TheoryData<string> Aggregates()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("aggregate"));
    }

    public static TheoryData<string> NonResults()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("none"));
    }

    public static TheoryData<string> Unsettled()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("unsettled"));
    }

    [Theory]
    [MemberData(nameof(Results))]
    public void AResultScreenReadsToTheNumbersOnItAndReconciles(string name)
    {
        var expected = FixtureScreens.Expected[name];
        var image = FixtureScreens.Load(name);

        var layout = _detector.Detect(image);
        Assert.Equal(Enum.Parse<ResultLayout>(expected.Layout!), layout);

        var reading = _reader.Read(image, layout!.Value);
        Assert.True(reading.Status == ReadingStatus.Complete, $"{reading.Status}: {reading.Reason}");
        Assert.Equal(expected.Mix, reading.Mix.ApiName());
        Assert.Equal(Enum.Parse<ChartType>(expected.ChartType!), reading.ChartType);
        Assert.Equal(expected.Level, reading.Level);
        Assert.Equal(
            Judgments.From(expected.Perfects!.Value, expected.Greats!.Value, expected.Goods!.Value, expected.Bads!.Value, expected.Misses!.Value),
            reading.Judgments);
        Assert.Equal(expected.MaxCombo, reading.MaxCombo);
        Assert.Equal(expected.Score, reading.Score);
        Assert.StartsWith(expected.Accuracy!.Replace(".", "", StringComparison.Ordinal), reading.AccuracyDigits, StringComparison.Ordinal);
        Assert.Equal(expected.Broken, reading.IsBroken);

        var verdict = PlayChecksum.Verify(reading);
        Assert.True(verdict.Reconciles, verdict.Problem);
    }

    [Theory]
    [MemberData(nameof(Unsettled))]
    public void AFrameCaughtWhileTheScoreStillCountsReadsButDoesNotReconcile(string name)
    {
        var expected = FixtureScreens.Expected[name];
        var image = FixtureScreens.Load(name);

        var reading = _reader.Read(image, _detector.Detect(image)!.Value);
        Assert.Equal(ReadingStatus.Complete, reading.Status);
        Assert.Equal(
            Judgments.From(expected.Perfects!.Value, expected.Greats!.Value, expected.Goods!.Value, expected.Bads!.Value, expected.Misses!.Value),
            reading.Judgments);
        Assert.Equal(expected.Level, reading.Level);
        Assert.Equal(expected.Score, reading.Score);
        Assert.False(PlayChecksum.Verify(reading).Reconciles);
    }

    [Theory]
    [MemberData(nameof(Empties))]
    public void AScreenWhoseNumbersHaveNotLandedIsNotReadAsAPlay(string name)
    {
        var image = FixtureScreens.Load(name);

        var layout = _detector.Detect(image);
        Assert.Equal(ResultLayout.DanceGrade, layout);
        Assert.Equal(ReadingStatus.NumbersNotShown, _reader.Read(image, layout!.Value).Status);
    }

    [Theory]
    [MemberData(nameof(Aggregates))]
    public void AChallengeAggregateIsNotAPlay(string name)
    {
        var image = FixtureScreens.Load(name);

        var layout = _detector.Detect(image);
        Assert.Equal(ResultLayout.DanceGrade, layout);
        Assert.Equal(ReadingStatus.NotAPlay, _reader.Read(image, layout!.Value).Status);
    }

    [Theory]
    [MemberData(nameof(NonResults))]
    public void NothingButAResultScreenDetects(string name)
    {
        Assert.Null(_detector.Detect(FixtureScreens.Load(name)));
    }

    [Fact]
    public void TheFixtureSetCoversBothLayoutsAndEveryNonPlayKind()
    {
        Assert.Contains(FixtureScreens.OfKind("result"), n => FixtureScreens.Expected[n].Layout == "Arcade");
        Assert.Contains(FixtureScreens.OfKind("result"), n => FixtureScreens.Expected[n].Layout == "DanceGrade");
        Assert.Contains(FixtureScreens.OfKind("result"), n => FixtureScreens.Expected[n].Broken == true);
        Assert.NotEmpty(FixtureScreens.OfKind("empty"));
        Assert.NotEmpty(FixtureScreens.OfKind("unsettled"));
        Assert.NotEmpty(FixtureScreens.OfKind("aggregate"));
        Assert.NotEmpty(FixtureScreens.OfKind("none"));
    }
}

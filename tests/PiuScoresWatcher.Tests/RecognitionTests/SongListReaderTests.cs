using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.RecognitionTests;

/// <summary>
///     The owner's Warm Up song-list screens through the real detector and reader (D45-D47): the lit chart,
///     its best, and the grade badge that checks it. The Arcade Station's list and every result screen are
///     not Warm Up's song list; no song list is a result screen.
/// </summary>
public sealed class SongListReaderTests
{
    private static readonly SongListReader Reader = new();

    public static TheoryData<string> Bests()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("songlist"));
    }

    public static TheoryData<string> NoBests()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("songlist-empty"));
    }

    public static TheoryData<string> NotWarmUpLists()
    {
        return new TheoryData<string>(FixtureScreens.Expected.Keys
            .Where(name => FixtureScreens.Expected[name].Kind is not ("songlist" or "songlist-empty"))
            .OrderBy(name => name, StringComparer.Ordinal));
    }

    public static TheoryData<string> SongLists()
    {
        return new TheoryData<string>(FixtureScreens.OfKind("songlist").Concat(FixtureScreens.OfKind("songlist-empty"))
            .Concat(FixtureScreens.OfKind("arcadelist")));
    }

    [Theory]
    [MemberData(nameof(Bests))]
    public void AWarmUpBestReadsToTheLitChartItsScoreAndAGradeThatAgrees(string name)
    {
        var expected = FixtureScreens.Expected[name];

        var reading = Reader.Read(FixtureScreens.Load(name));

        Assert.NotNull(reading);
        Assert.Equal(SongListStatus.Best, reading.Status);
        Assert.Equal(Enum.Parse<ChartType>(expected.ChartType!), reading.ChartType);
        Assert.Equal(expected.Level, reading.Level);
        Assert.Equal(expected.Score, reading.Score);
        Assert.Equal(expected.Grade, reading.Grade);
    }

    /// <summary>
    ///     Whatever the lit box reads: both fixtures caught the highlight moving, with the new box's digits
    ///     still grey (on one of them the two digits touch after compression). A chart with no best is
    ///     skipped either way, and a live run reads the box only once the panel has settled (D48).
    /// </summary>
    [Theory]
    [MemberData(nameof(NoBests))]
    public void AChartWithNoBestIsNothingToCapture(string name)
    {
        var expected = FixtureScreens.Expected[name];

        var reading = Reader.Read(FixtureScreens.Load(name));

        Assert.NotNull(reading);
        Assert.Equal(SongListStatus.NoBest, reading.Status);
        Assert.Equal(Enum.Parse<ChartType>(expected.ChartType!), reading.ChartType);
        Assert.Null(reading.Score);
    }

    [Theory]
    [MemberData(nameof(NotWarmUpLists))]
    public void NothingElseIsWarmUpsSongList(string name)
    {
        Assert.Null(new SongListDetector().Detect(FixtureScreens.Load(name)));
    }

    [Theory]
    [MemberData(nameof(SongLists))]
    public void NoSongListIsAResultScreen(string name)
    {
        Assert.Null(new ResultScreenDetector().Detect(FixtureScreens.Load(name)));
    }

    [Fact]
    public void TheLitSongsJacketTellsSongsApartAndHoldsWhileTheLevelChanges()
    {
        // Aragami's S19 and, two seconds later, its S17; the other five are five other songs
        Assert.Equal(Reader.Read(FixtureScreens.Load("20260923193118"))!.Jacket, Reader.Read(FixtureScreens.Load("20260923193120"))!.Jacket);

        var songs = FixtureScreens.OfKind("songlist").Concat(FixtureScreens.OfKind("songlist-empty"))
            .Where(name => name != "20260923193120")
            .Select(name => Reader.Read(FixtureScreens.Load(name))!.Jacket)
            .ToList();
        Assert.Equal(6, songs.Count);
        Assert.Equal(songs.Count, songs.Distinct().Count());
    }

    [Fact]
    public void TheFixturesCoverEveryCase()
    {
        Assert.NotEmpty(FixtureScreens.OfKind("songlist"));
        Assert.NotEmpty(FixtureScreens.OfKind("songlist-empty"));
        Assert.NotEmpty(FixtureScreens.OfKind("arcadelist"));
        // two of the bests were taken while the panel faded in: grey digits, a dimmer badge
        Assert.Contains(FixtureScreens.OfKind("songlist"), n => FixtureScreens.Expected[n].Grade == "S");
    }
}

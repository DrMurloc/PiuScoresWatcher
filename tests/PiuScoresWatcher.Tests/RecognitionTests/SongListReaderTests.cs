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
        // Aragami's S19 and, two seconds later, its S17; the tester's Quick Brown Fox at S11 and S19, from two sorts of
        // the list; every other screen is another song
        var jackets = FixtureScreens.OfKind("songlist").Concat(FixtureScreens.OfKind("songlist-empty"))
            .Select(name => (FixtureScreens.Expected[name].Title, Reader.Read(FixtureScreens.Load(name))!.Jacket))
            .ToList();

        Assert.All(jackets.GroupBy(screen => screen.Title), song => Assert.Single(song.Select(screen => screen.Jacket).Distinct()));
        Assert.Equal(jackets.Select(screen => screen.Title).Distinct().Count(), jackets.Select(screen => screen.Jacket).Distinct().Count());
    }

    [Fact]
    public void TheTitleIsReadInTheListsLitRowAndThenOnThePanel()
    {
        var titles = Reader.Read(FixtureScreens.Load("20260923193118"))!.Titles;

        Assert.Equal(["list", "panel"], titles.Select(box => box.Name));
        Assert.False(titles[0].Heavy);
        Assert.True(titles[1].Heavy);
        Assert.All(titles, box => Assert.NotNull(box.ScrollsPast));
    }

    [Theory]
    [MemberData(nameof(Bests))]
    public void EveryBestsTitleIsInTheListsLitRow(string name)
    {
        // the list keeps the lit song in its fourth row, as it does its jacket (D48, D66): a title is there, from B2 up
        var image = FixtureScreens.Load(name);

        var page = TitleInk.Render(image, Reader.Read(image)!.Titles[0].Region);

        Assert.InRange(page.Pixels.Count(shade => shade < 128) / (double)page.Pixels.Length, 0.005, 0.5);
    }

    [Fact]
    public void ATitleScrolledOffThePanelIsStillWholeInTheList()
    {
        // Blaze emotion (Band version): the panel's ticker between two passes of the title
        var image = FixtureScreens.Load("20260924234156");
        var titles = Reader.Read(image)!.Titles;

        var panel = TitleInk.Render(image, titles[1].Region);
        var row = TitleInk.Render(image, titles[0].Region);

        Assert.True(panel.Pixels.Count(shade => shade < 128) < 0.002 * panel.Pixels.Length);
        Assert.True(row.Pixels.Count(shade => shade < 128) > 0.05 * row.Pixels.Length);
        Assert.False(TitleInk.RunsOffRight(image, titles[0].Region));
    }

    [Fact]
    public void TheFixturesCoverEveryCase()
    {
        Assert.NotEmpty(FixtureScreens.OfKind("songlist"));
        Assert.NotEmpty(FixtureScreens.OfKind("songlist-empty"));
        Assert.NotEmpty(FixtureScreens.OfKind("arcadelist"));
        // two of the bests were taken while the panel faded in: grey digits, a dimmer badge
        Assert.Contains(FixtureScreens.OfKind("songlist"), n => FixtureScreens.Expected[n].Grade == "S");
        // each tab lights in its own colour, 5K SINGLE orange and 6K DOUBLE blue (D71)
        Assert.All(new[] { nameof(ChartType.Single), nameof(ChartType.HalfDouble) },
            type => Assert.Contains(FixtureScreens.OfKind("songlist"), n => FixtureScreens.Expected[n].ChartType == type));
    }
}

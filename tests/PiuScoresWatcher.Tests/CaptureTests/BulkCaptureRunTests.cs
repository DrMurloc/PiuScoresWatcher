using Moq;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.CaptureTests;

/// <summary>
///     A bulk capture run over the owner's Warm Up song-list screens (D45-D52): the real reader, the site,
///     the title reader and the kept-screen store as doubles, the clock a test moves.
/// </summary>
public sealed class BulkCaptureRunTests
{
    private const string Aragami = "20260923193118";   // 5K S19, best 971,789, SS
    private const string Morrighan = "20260923193156"; // 5K S20, best 945,403, AA
    private const string NoBestHere = "20260923193120"; // Aragami 5K S17, no best
    private const string VacuumCleaner = "20260923193144"; // 5K S20, best 956,984, S
    private const string AResultScreen = "20260921201328";
    private const string SixKDouble = "20260926111534"; // 6K HD16, best 972,054, SS (D71)

    private static readonly Guid AragamiS19 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MorrighanS20 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VacuumCleanerS20 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 19, 31, 18, TimeSpan.FromHours(-4));

    /// <summary>
    ///     6K DOUBLE's list with that tab taken from Aragami's 5K SINGLE screen, where it is unlit: a lit box and no tab the
    ///     detector knows, the list as it saw 6K DOUBLE before D71 (D72).
    /// </summary>
    private static readonly Lazy<ScreenImage> Unplaced =
        new(() => FixtureScreens.LoadWithRegionOf(SixKDouble, Aragami, FractionRect.At1080p(398, 492, 607, 540)));

    private readonly FakeClock _clock = FakeClock.At(Start);
    private readonly Mock<IFailedScreenStore> _failed = new();
    private readonly Mock<IPlaysClient> _site = new();
    private readonly Mock<ITitleReader> _titles = new();

    private readonly SongCatalog _catalog = new(
    [
        new CatalogChart(AragamiS19, "Aragami", ChartType.Single, 19),
        new CatalogChart(MorrighanS20, "Morrighan", ChartType.Single, 20),
        new CatalogChart(VacuumCleanerS20, "Vacuum Cleaner", ChartType.Single, 20)
    ]);

    public BulkCaptureRunTests()
    {
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Recorded(1, "Rise"));
        _failed.Setup(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()))
            .Returns(@"C:\failed\list.png");
        TitleIs("Aragami");
    }

    private void TitleIs(string title)
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<TitleBox>(), It.IsAny<CancellationToken>())).Returns(() => TitleReads.Of(title));
    }

    private BulkCaptureRun Run(params StoredBest[] bests)
    {
        return new BulkCaptureRun(new SongListReader(), _titles.Object, _site.Object, _failed.Object, _clock, _catalog, bests);
    }

    private CapturedFrame Window(string fixture)
    {
        return new CapturedFrame(FixtureScreens.Load(fixture), CaptureSource.GameWindow, _clock.Now, null);
    }

    private CapturedFrame Screenshot(string fixture)
    {
        return Screenshot(FixtureScreens.Load(fixture));
    }

    private CapturedFrame Screenshot(ScreenImage image)
    {
        return new CapturedFrame(image, CaptureSource.SteamScreenshot, _clock.Now, "shot.jpg");
    }

    /// <summary>The same window frame until the panel has been still for the half second (D48).</summary>
    private Task<BulkOutcome> SettleAsync(BulkCaptureRun run, string fixture)
    {
        return SettleAsync(run, FixtureScreens.Load(fixture));
    }

    private async Task<BulkOutcome> SettleAsync(BulkCaptureRun run, ScreenImage image)
    {
        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(image), CancellationToken.None));
        _clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(image), CancellationToken.None));
        _clock.Advance(TimeSpan.FromMilliseconds(250));
        return await run.HandleAsync(Window(image), CancellationToken.None);
    }

    private CapturedFrame Window(ScreenImage image)
    {
        return new CapturedFrame(image, CaptureSource.GameWindow, _clock.Now, null);
    }

    [Fact]
    public async Task ABestHigherThanTheSitesIsSentOnceThePanelHasBeenStillForHalfASecond()
    {
        var run = Run(new StoredBest(AragamiS19, 950_000, false));

        var sent = Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Aragami));

        Assert.Equal("Aragami", sent.Play.SongName);
        Assert.Equal(19, sent.Play.Level);
        Assert.Equal(971789, sent.Play.Score);
        Assert.Null(sent.Play.Judgments);
        Assert.False(sent.Play.IsBroken);
        _site.Verify(s => s.PostAsync(It.Is<ObservedPlay>(p => p.Mix == RiseMix.Rise && p.Judgments == null), "watcher-songlist",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, run.Tally.Sent);
    }

    [Fact]
    public async Task AChartIsActedOnOncePerArrival()
    {
        var run = Run();
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Aragami));

        _clock.Advance(TimeSpan.FromSeconds(3));
        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(Aragami), CancellationToken.None));

        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheNextSongIsActedOnWhenItsPanelReadsTheSameAsTheLast()
    {
        // Morrighan's panel with the next song's jacket lit in the list: the same level, best and grade, another song
        var run = Run();
        TitleIs("Morrighan");
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Morrighan));

        TitleIs("Vacuum Cleaner");
        var next = FixtureScreens.LoadWithRegionOf(Morrighan, VacuumCleaner, FractionRect.At1080p(760, 555, 880, 665));
        var sent = Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, next));

        Assert.Equal("Vacuum Cleaner", sent.Play.SongName);
        Assert.Equal(945403, sent.Play.Score);
    }

    [Fact]
    public async Task ComingBackToAChartSentEarlierIsAlreadyThere()
    {
        var run = Run();
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Aragami));
        TitleIs("Morrighan");
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Morrighan));
        TitleIs("Aragami");

        Assert.IsType<BulkOutcome.AlreadyThere>(await SettleAsync(run, Aragami));

        Assert.Equal(new BulkTally(2, 1, 0, 0), run.Tally);
    }

    [Theory]
    [InlineData(971_789)]
    [InlineData(985_000)]
    public async Task ABestTheSiteAlreadyHasOrBeatsIsNotSent(int stored)
    {
        var run = Run(new StoredBest(AragamiS19, stored, false));

        var already = Assert.IsType<BulkOutcome.AlreadyThere>(await SettleAsync(run, Aragami));

        Assert.Equal("Aragami", already.SongName);
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABrokenBestOnTheSiteIsReplacedByThePass()
    {
        var run = Run(new StoredBest(AragamiS19, 990_000, true));

        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Aragami));
    }

    /// <summary>The same window frame after <paramref name="elapsed" />: the panel holds while its title is read again (D69).</summary>
    private Task<BulkOutcome> AgainAsync(BulkCaptureRun run, string fixture, TimeSpan elapsed)
    {
        return AgainAsync(run, FixtureScreens.Load(fixture), elapsed);
    }

    private async Task<BulkOutcome> AgainAsync(BulkCaptureRun run, ScreenImage image, TimeSpan elapsed)
    {
        _clock.Advance(elapsed);
        return await run.HandleAsync(Window(image), CancellationToken.None);
    }

    [Fact]
    public async Task ATitleThatNamesNoChartIsReadAgainWhileThePanelHoldsAndThenKeptNotSent()
    {
        TitleIs("Something Else");
        var run = Run();

        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(1)));
        var kept = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(1)));

        Assert.Equal(KeptBecause.TitleUnmatched, kept.Because);
        Assert.Contains("'Something Else' (list)", kept.Reason);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TitleUnmatched, It.IsAny<string>(), null), Times.Once);
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, run.Tally.NotSent);
        // three tries, each in the list's row and then on the panel
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<TitleBox>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task ATitleThatNamesTheChartOnALaterFrameIsSent()
    {
        // the ticker showed a piece that named nothing, then the start of the title
        TitleIs("wanna go to t");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        TitleIs("Aragami");
        var sent = Assert.IsType<BulkOutcome.Sent>(await AgainAsync(run, Aragami, TimeSpan.FromMilliseconds(200)));

        Assert.Equal("Aragami", sent.Play.SongName);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()),
            Times.Never);
    }

    [Fact]
    public async Task APanelLeftWhileItsTitleNamesNothingIsKeptAsItWasLastSeen()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        TitleIs("Morrighan");
        var kept = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Morrighan, TimeSpan.FromMilliseconds(200)));

        Assert.Equal(KeptBecause.TitleUnmatched, kept.Because);
        Assert.Contains("Single 19", kept.Reason); // Aragami's panel, not Morrighan's
        // and the new panel's half second has begun
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Morrighan, TimeSpan.FromMilliseconds(250)));
        Assert.IsType<BulkOutcome.Sent>(await AgainAsync(run, Morrighan, TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task TheListLeftWhileATitleNamesNothingKeepsItAndLetsTheFrameThrough()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        var notTheList = Assert.IsType<BulkOutcome.NotTheList>(await AgainAsync(run, AResultScreen, TimeSpan.FromMilliseconds(200)));

        Assert.Equal(KeptBecause.TitleUnmatched, Assert.IsType<BulkOutcome.Unreadable>(notTheList.Kept).Because);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TitleUnmatched, It.IsAny<string>(), null), Times.Once);
        Assert.Null((await run.HandleAsync(Window(AResultScreen), CancellationToken.None) as BulkOutcome.NotTheList)!.Kept);
    }

    [Fact]
    public async Task TheRunEndingWhileATitleNamesNothingKeepsIt()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        var kept = run.End();

        Assert.Equal(KeptBecause.TitleUnmatched, kept!.Because);
        Assert.Equal(1, run.Tally.Unreadable);
        Assert.Null(run.End());
    }

    [Fact]
    public async Task AChartIsKeptOnceInARunHoweverOftenItIsComeBackTo()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));
        Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(2)));
        TitleIs("Morrighan");
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Morrighan));

        TitleIs("Something Else");
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));
        var again = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(2)));

        Assert.Null(again.SavedTo); // heard, not kept twice
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TitleUnmatched, It.IsAny<string>(), null), Times.Once);
        Assert.Equal(new BulkTally(1, 0, 1, 0), run.Tally);
    }

    [Fact]
    public async Task ASongTheListHasOnlyAtOtherChartsIsKeptForThatReason()
    {
        // the game's Elysium S4 is PIU Scores' S3: here, Aragami's S19 is missing from the list
        TitleIs("Aragami");
        var run = new BulkCaptureRun(new SongListReader(), _titles.Object, _site.Object, _failed.Object, _clock,
            new SongCatalog([new CatalogChart(Guid.NewGuid(), "Aragami", ChartType.Single, 17)]), []);

        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));
        var kept = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(2)));

        Assert.Equal(KeptBecause.ChartUnlisted, kept.Because);
        Assert.Contains("names Aragami, which PIU Scores doesn't list at Single 19", kept.Reason);
    }

    [Fact]
    public async Task AScreenshotWhoseTitleNamesNothingIsKeptAtOnce()
    {
        TitleIs("Something Else");
        var run = Run();

        Assert.IsType<BulkOutcome.Unreadable>(await run.HandleAsync(Screenshot(Aragami), CancellationToken.None));
    }

    [Fact]
    public async Task AScreenshotLeavesTheWindowsRetryAlone()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        TitleIs("Morrighan");
        Assert.IsType<BulkOutcome.Sent>(await run.HandleAsync(Screenshot(Morrighan), CancellationToken.None));

        TitleIs("Something Else");
        Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(2)));
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TitleUnmatched, It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task ACaptureTheSiteDoesNotRecordIsKept()
    {
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Refused("judgments-invalid", null));
        var run = Run();

        var notRecorded = Assert.IsType<BulkOutcome.NotRecorded>(await SettleAsync(run, Aragami));

        Assert.Equal(@"C:\failed\list.png", notRecorded.SavedTo);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.Refused, It.IsAny<string>(), null), Times.Once);
        Assert.Equal(new BulkTally(0, 0, 0, 1), run.Tally);
    }

    [Fact]
    public async Task ASteamScreenshotIsAlreadyStill()
    {
        var run = Run();

        Assert.IsType<BulkOutcome.Sent>(await run.HandleAsync(Screenshot(Aragami), CancellationToken.None));
    }

    [Fact]
    public async Task AChartWithNoBestIsSkippedQuietly()
    {
        var run = Run();

        Assert.IsType<BulkOutcome.NoBest>(await SettleAsync(run, NoBestHere));

        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<TitleBox>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(BulkTally.None, run.Tally);
    }

    [Fact]
    public async Task AListWhoseLitChartCantBePlacedIsKeptOnceItHasStayedSoForTwoSeconds()
    {
        var run = Run();

        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(Unplaced.Value), CancellationToken.None));
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1)));
        var kept = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1)));

        Assert.Equal(KeptBecause.ChartUnplaced, kept.Because);
        Assert.Equal(@"C:\failed\list.png", kept.SavedTo);
        Assert.StartsWith("the song list with no tab lit", kept.Reason);
        Assert.Equal(new BulkTally(0, 0, 1, 0), run.Tally);
        // on screen all along: the run doesn't end for the list being gone
        Assert.Equal(_clock.Now, run.ListLastSeen);
    }

    [Fact]
    public async Task AListWhoseLitChartCantBePlacedIsKeptOnceInARunAndThenNothingMoreIsSaid()
    {
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(Unplaced.Value), CancellationToken.None));
        Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(2)));
        Assert.IsType<BulkOutcome.Sent>(await SettleAsync(run, Aragami));

        for (var i = 0; i < 4; i++)
            Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1)));
        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Screenshot(Unplaced.Value), CancellationToken.None));

        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.ChartUnplaced, It.IsAny<string>(), null), Times.Once);
        Assert.Equal(new BulkTally(1, 0, 1, 0), run.Tally);
    }

    [Fact]
    public async Task AListPlacedAgainWithinTwoSecondsStartsThemOver()
    {
        var run = Run();

        Assert.IsType<BulkOutcome.Waiting>(await run.HandleAsync(Window(Unplaced.Value), CancellationToken.None));
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Aragami, TimeSpan.FromSeconds(1.5)));
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1)));
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1)));

        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.ChartUnplaced, It.IsAny<string>(), null), Times.Never);
    }

    [Fact]
    public async Task AScreenshotOfAListWhoseLitChartCantBePlacedIsKeptAtOnce()
    {
        var run = Run();

        var kept = Assert.IsType<BulkOutcome.Unreadable>(await run.HandleAsync(Screenshot(Unplaced.Value), CancellationToken.None));

        Assert.Equal(KeptBecause.ChartUnplaced, kept.Because);
    }

    [Fact]
    public async Task AChartWhoseTitleNamesNothingIsKeptWhenTheListCanNoLongerBePlaced()
    {
        TitleIs("Something Else");
        var run = Run();
        Assert.IsType<BulkOutcome.Waiting>(await SettleAsync(run, Aragami));

        var kept = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromMilliseconds(200)));

        Assert.Equal(KeptBecause.TitleUnmatched, kept.Because); // Aragami, as it was last seen
        Assert.IsType<BulkOutcome.Waiting>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(1.9)));
        var unplaced = Assert.IsType<BulkOutcome.Unreadable>(await AgainAsync(run, Unplaced.Value, TimeSpan.FromSeconds(0.1)));
        Assert.Equal(KeptBecause.ChartUnplaced, unplaced.Because);
    }

    [Fact]
    public async Task AFrameThatIsNotTheListLeavesTheRunAlone()
    {
        var run = Run();

        Assert.IsType<BulkOutcome.NotTheList>(await run.HandleAsync(Window(AResultScreen), CancellationToken.None));

        Assert.Null(run.ListLastSeen);
    }

    [Fact]
    public async Task TheListBeingOnScreenIsRemembered()
    {
        var run = Run();

        await run.HandleAsync(Window(Aragami), CancellationToken.None);

        Assert.Equal(Start, run.ListLastSeen);
    }

    [Fact]
    public void TheStartWindowCountsTheBestsTheSiteHolds()
    {
        Assert.Equal(1, Run(new StoredBest(AragamiS19, 971789, false), new StoredBest(MorrighanS20, null, true)).StoredBestCount);
    }

    // ---- The other rows' Perfect Games (D84-D86), on the first tester's lists: 5K SINGLE, rows full of three lit bars ----

    private const string B2List = "20260924233951";     // B2 S7 lit at 1,000,000; Perfect Games in rows 1-3, 6 and 7
    private const string PeopleList = "20260925003656"; // The People didn't know "Pumping up" S8 lit
    private const string FoxList = "20260925003727";    // the same list one song on, The Quick Brown Fox's S19 lit

    private static readonly (string Song, int[] Levels)[] TesterSongs =
    [
        ("B2", [4, 7, 10, 16, 18]), ("Awakening", [7, 10, 14, 17, 19, 21]), ("B.P Classic Remix", [14, 18]),
        ("B.P Classic Remix 2", [13, 18]), ("Banya Classic Remix", [13, 19]), ("BANYA HIPHOP REMIX", [6, 8]),
        ("The Devil", [4, 6, 9, 12, 19]), ("The Last Stand", [4, 7, 11, 16, 19, 21]), ("The People didn't know", [3, 5, 12, 16]),
        ("The People didn't know \"Pumping up\"", [8]), ("The Quick Brown Fox Jumps Over The Lazy Dog", [11, 16, 19, 23]),
        ("Till the end of time", [3, 4, 11, 13, 17]), ("Time wanderer", [12, 16, 19, 21])
    ];

    /// <summary>Each fixture's titles as Windows OCR reads them in --replay: the lit song's, and each row's with a Perfect Game.</summary>
    private static readonly Dictionary<string, (string Lit, Dictionary<int, string> Rows)> TesterTitles = new()
    {
        [B2List] = ("B2", new() { [1] = "Awakening", [2] = "B.P Classic Remix", [3] = "B.P Classic Remix 2", [6] = "Banya Classic Remix", [7] = "BANYA HIPHOP REMIX" }),
        [PeopleList] = ("The People didn't know \"Pumping up\"", new()
        {
            [1] = "The Devil", [2] = "The Last Stand", [3] = "The People didn't know", [5] = "The Quick Brown Fox Jumps Over The La", [7] = "Till the end of time"
        }),
        [FoxList] = ("", new() { [1] = "The Last Stand", [2] = "The People didn't know", [3] = "The People didn't know Pumping up", [6] = "Till the end of time", [7] = "Time wanderer" })
    };

    private readonly Dictionary<(string Song, int Level), CatalogChart> _testerCharts = TesterSongs
        .SelectMany(song => song.Levels.Select(level => new CatalogChart(Guid.NewGuid(), song.Song, ChartType.Single, level)))
        .ToDictionary(chart => (chart.SongName, chart.Level));

    private BulkCaptureRun TesterRun(params (string Song, int Level, int Score)[] bests)
    {
        return new BulkCaptureRun(new SongListReader(), _titles.Object, _site.Object, _failed.Object, _clock, new SongCatalog(_testerCharts.Values),
            bests.Select(best => new StoredBest(_testerCharts[(best.Song, best.Level)].Id, best.Score, false)));
    }

    /// <summary>
    ///     The titles a fixture's boxes read: each row's with a Perfect Game, and the lit song's in both of its boxes — or
    ///     nothing there, so the lit chart is kept rather than sent and the rows are all a test hears about.
    /// </summary>
    private void TitlesOf(string fixture, bool litNamed = true)
    {
        var (lit, rows) = TesterTitles[fixture];
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<TitleBox>(), It.IsAny<CancellationToken>()))
            .Returns(() => litNamed && lit.Length > 0 ? TitleReads.Of(lit) : TitleReads.Of());
        foreach (var (row, title) in rows)
            _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == $"row {row}"), It.IsAny<CancellationToken>()))
                .Returns(() => TitleReads.Of(title));
    }

    private void VerifyPerfectGameSent(string song, int level)
    {
        _site.Verify(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == song && p.Level == level && p.Score == 1_000_000 && p.Judgments == null
                                                                && p.ChartType == ChartType.Single && p.Mix == RiseMix.Rise),
            "watcher-songlist", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IReadOnlyList<(string Song, int Level)> SentOf(BulkOutcome outcome)
    {
        return outcome.PerfectGames.OfType<PerfectGameOutcome.Sent>().Select(sent => (sent.Play.SongName, sent.Play.Level)).ToList();
    }

    [Fact]
    public async Task EveryPerfectGameInTheOtherRowsIsSentOnceTheListHasHeldStillAFrameAfterTheLitChartsSound()
    {
        // the lit B2 S7 is already on PIU Scores: its tick comes first, the rows' Perfect Games a frame later (D87)
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);

        var lit = Assert.IsType<BulkOutcome.AlreadyThere>(await SettleAsync(run, B2List));
        Assert.Empty(lit.PerfectGames);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        var rows = await run.HandleAsync(Window(B2List), CancellationToken.None);

        Assert.IsType<BulkOutcome.Waiting>(rows);
        Assert.Equal(
        [
            ("Awakening", 7), ("Awakening", 10), ("Awakening", 14), ("B.P Classic Remix", 14), ("B.P Classic Remix 2", 13),
            ("Banya Classic Remix", 13), ("BANYA HIPHOP REMIX", 6), ("BANYA HIPHOP REMIX", 8)
        ], SentOf(rows));
        VerifyPerfectGameSent("Awakening", 10);
        VerifyPerfectGameSent("BANYA HIPHOP REMIX", 8);
        Assert.Equal(new BulkTally(8, 1, 0, 0, 8), run.Tally);
    }

    [Fact]
    public async Task RowsThatHeldStillGoUpWhenTheListMovesOnAtTheLitChartsSound()
    {
        // the tick for B2 S7, and S at once: the next frame is the list one song on, still moving (owner, 2026-09-26: "i'm
        // gonna want to hit that button as soon as i hear that noise start")
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        Assert.IsType<BulkOutcome.AlreadyThere>(await SettleAsync(run, B2List));
        _clock.Advance(TimeSpan.FromMilliseconds(200));

        var next = await run.HandleAsync(Window(PeopleList), CancellationToken.None);

        Assert.IsType<BulkOutcome.Waiting>(next);
        Assert.Equal(8, SentOf(next).Count);
        Assert.Contains(("BANYA HIPHOP REMIX", 8), SentOf(next));
    }

    [Fact]
    public async Task ASweepThatMovesOnAtEverySoundSendsEveryRowsPerfectGame()
    {
        // The People didn't know "Pumping up", one song on to The Quick Brown Fox (whose title names nothing, so its two
        // seconds run out on the low tone), then B2, moving on the moment each makes its sound, and a song started at the
        // end: the bug check sent none of these 26 before the rows waited with their frames
        var run = TesterRun(("The People didn't know \"Pumping up\"", 8, 1_000_000), ("B2", 7, 1_000_000));
        var screens = new[] { PeopleList, FoxList, B2List }.ToDictionary(name => name, FixtureScreens.Load);
        foreach (var (name, image) in screens)
            TitlesIn(name, image);

        List<BulkOutcome> outcomes = [];
        foreach (var image in screens.Values)
            outcomes.AddRange(await UntilTheSoundAsync(run, image));
        outcomes.Add(await run.HandleAsync(Window(AResultScreen), CancellationToken.None));

        Assert.Equal(26, outcomes.Sum(outcome => SentOf(outcome).Count));
        Assert.Equal(26, run.Tally.PerfectGames);
    }

    /// <summary>A fixture's titles, as <see cref="TitlesOf" /> has them, read only off that frame: rows taken up on one screen are read on the next.</summary>
    private void TitlesIn(string fixture, ScreenImage image)
    {
        var (lit, rows) = TesterTitles[fixture];
        _titles.Setup(t => t.ReadAsync(image, It.Is<TitleBox>(box => box.Name == "list" || box.Name == "panel"), It.IsAny<CancellationToken>()))
            .Returns(() => lit.Length > 0 ? TitleReads.Of(lit) : TitleReads.Of());
        foreach (var (row, title) in rows)
            _titles.Setup(t => t.ReadAsync(image, It.Is<TitleBox>(box => box.Name == $"row {row}"), It.IsAny<CancellationToken>()))
                .Returns(() => TitleReads.Of(title));
    }

    /// <summary>A screen's frames, 200 ms apart, until its lit chart makes its sound, and not a frame more: the player moves on at the sound.</summary>
    private async Task<List<BulkOutcome>> UntilTheSoundAsync(BulkCaptureRun run, ScreenImage image)
    {
        List<BulkOutcome> outcomes = [];
        while (outcomes.Count < 20)
        {
            var outcome = await run.HandleAsync(Window(image), CancellationToken.None);
            outcomes.Add(outcome);
            _clock.Advance(TimeSpan.FromMilliseconds(200));
            if (outcome is not (BulkOutcome.Waiting or BulkOutcome.NoBest))
                return outcomes;
        }

        throw new InvalidOperationException("The lit chart never made its sound.");
    }

    [Fact]
    public async Task RowsThatHeldStillGoUpWhenTheListGoesAwayAtTheLitChartsSound()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        Assert.IsType<BulkOutcome.AlreadyThere>(await SettleAsync(run, B2List));
        _clock.Advance(TimeSpan.FromMilliseconds(200));

        var gone = await run.HandleAsync(Window(AResultScreen), CancellationToken.None);

        Assert.IsType<BulkOutcome.NotTheList>(gone);
        Assert.Equal(8, SentOf(gone).Count);
    }

    [Fact]
    public async Task RowsWaitWhileTheLitChartReadsItsTitleAgainSoTheirPostsDontEatItsTwoSeconds()
    {
        // B2's title names nothing on its first two reads and B2 on its third (D69), and each post takes 300 ms: the rows'
        // eight would take 2.4 s of the lit chart's two seconds
        var run = TesterRun();
        TitlesOf(B2List);
        _titles.SetupSequence(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "list"), It.IsAny<CancellationToken>()))
            .Returns(TitleReads.Of()).Returns(TitleReads.Of()).Returns(TitleReads.Of("B2"));
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "panel"), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of());
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _clock.Advance(TimeSpan.FromMilliseconds(300));
                return new PostOutcome.Recorded(1, "Rise");
            });

        var reading = await SettleAsync(run, B2List);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        var again = await run.HandleAsync(Window(B2List), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        var named = await run.HandleAsync(Window(B2List), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        var rows = await run.HandleAsync(Window(B2List), CancellationToken.None);

        Assert.Empty(reading.PerfectGames);
        Assert.Empty(again.PerfectGames);
        Assert.Equal("B2", Assert.IsType<BulkOutcome.Sent>(named).Play.SongName);
        Assert.Empty(named.PerfectGames);
        Assert.Equal(8, SentOf(rows).Count);
    }

    [Fact]
    public async Task ARowLostToTheNetworkIsTakenUpAgainAfterAScreenWithoutPerfectGames()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        _site.SetupSequence(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == "BANYA HIPHOP REMIX" && p.Level == 8), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Failed(null, "no network"))
            .ReturnsAsync(new PostOutcome.Recorded(1, "Rise"));
        await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        // away to the owner's list, whose rows carry no marks, and back
        await run.HandleAsync(Screenshot(Aragami), CancellationToken.None);
        var back = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        Assert.Equal([("BANYA HIPHOP REMIX", 8)], SentOf(back));
    }

    [Fact]
    public async Task RowsThatHaveNotHeldStillForHalfASecondSendNothing()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        await run.HandleAsync(Window(B2List), CancellationToken.None);
        _clock.Advance(TimeSpan.FromMilliseconds(400));

        var outcome = await run.HandleAsync(Window(B2List), CancellationToken.None);

        Assert.Empty(outcome.PerfectGames);
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APerfectGamePIUScoresAlreadyHasIsNeitherSentNorCounted()
    {
        var run = TesterRun(("B2", 7, 1_000_000), ("Awakening", 10, 1_000_000), ("BANYA HIPHOP REMIX", 8, 993_000));
        TitlesOf(B2List);

        var outcome = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        Assert.Contains(new PerfectGameOutcome.AlreadyThere("Awakening", ChartType.Single, 10), outcome.PerfectGames);
        Assert.Contains(("BANYA HIPHOP REMIX", 8), SentOf(outcome));
        Assert.Equal(7, SentOf(outcome).Count);
        Assert.Equal(new BulkTally(7, 1, 0, 0, 7), run.Tally);
    }

    [Fact]
    public async Task AScreenshotSendsTheRowsWithTheLitChart()
    {
        var run = TesterRun();
        TitlesOf(B2List);

        var outcome = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        var lit = Assert.IsType<BulkOutcome.Sent>(outcome);
        Assert.True(lit.Play.IsPerfectGame);
        Assert.Equal(8, SentOf(outcome).Count);
        Assert.Equal(new BulkTally(9, 0, 0, 0, 9), run.Tally);
    }

    [Fact]
    public async Task ARowIsActedOnOnceARunWhereverItScrollsTo()
    {
        // one song on from The People didn't know "Pumping up" to The Quick Brown Fox: three rows moved up one place with
        // their jackets and Perfect Games, and only the two that weren't on screen before are read and sent
        var run = TesterRun();
        TitlesOf(PeopleList, litNamed: false);
        await run.HandleAsync(Screenshot(PeopleList), CancellationToken.None);
        TitlesOf(FoxList);

        var outcome = await run.HandleAsync(Screenshot(FoxList), CancellationToken.None);

        Assert.Equal([("The People didn't know \"Pumping up\"", 8), ("Time wanderer", 12), ("Time wanderer", 16)], SentOf(outcome));
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "row 6"), It.IsAny<CancellationToken>()), Times.Never);
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "row 1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheSameRowsOnTheOtherTabAreActedOnAgain()
    {
        // B2's list with 6K DOUBLE lit instead, the rest of the screen as it was: after TAB the same songs, marks alike,
        // are other charts — here ones PIU Scores' list doesn't have, so each is left, but each is looked at
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        Assert.Equal(8, (await run.HandleAsync(Screenshot(B2List), CancellationToken.None)).PerfectGames.Count);
        var unlit = FixtureScreens.Painted(FixtureScreens.Load(B2List), FractionRect.At1080p(174, 492, 383, 540), 80, 80, 80);
        var sixK = FixtureScreens.Painted(unlit, FractionRect.At1080p(398, 492, 607, 540), 40, 170, 240);

        var outcome = await run.HandleAsync(Screenshot(sixK), CancellationToken.None);

        Assert.Equal(8, outcome.PerfectGames.Count);
        Assert.All(outcome.PerfectGames, perfectGame => Assert.Equal(ChartType.HalfDouble, Assert.IsType<PerfectGameOutcome.Unplaced>(perfectGame).ChartType));
    }

    [Fact]
    public async Task ARowSeenAgainWithTheListStillIsNotReadAgain()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(1));
        foreach (var _ in Enumerable.Range(0, 5))
        {
            Assert.Empty((await run.HandleAsync(Window(B2List), CancellationToken.None)).PerfectGames);
            _clock.Advance(TimeSpan.FromMilliseconds(200));
        }

        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "row 1"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(8, run.Tally.PerfectGames);
    }

    [Fact]
    public async Task ARowWhoseTitleNamesNoChartIsLeftQuietlyAndNothingIsKept()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(box => box.Name == "row 1"), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of("Awakeing Rising Dawn"));

        var outcome = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        Assert.Equal(3, outcome.PerfectGames.OfType<PerfectGameOutcome.Unplaced>().Count(unplaced => unplaced.Row == 1));
        Assert.Equal(5, SentOf(outcome).Count);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Never);
        Assert.Equal(new BulkTally(5, 1, 0, 0, 5), run.Tally);
    }

    [Fact]
    public async Task APerfectGameTheListLacksIsLeftQuietly()
    {
        // Awakening named at S7; PIU Scores' list has no S10 of it (D70)
        _testerCharts.Remove(("Awakening", 10));
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);

        var outcome = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        var unplaced = Assert.Single(outcome.PerfectGames.OfType<PerfectGameOutcome.Unplaced>());
        Assert.Equal((1, 10), (unplaced.Row, unplaced.Level));
        Assert.Contains(("Awakening", 14), SentOf(outcome));
    }

    [Fact]
    public async Task APerfectGamePIUScoresRefusesIsLeftQuietlyAndNotSentAgain()
    {
        var run = TesterRun(("B2", 7, 1_000_000), ("The People didn't know \"Pumping up\"", 8, 1_000_000));
        _site.Setup(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == "Awakening"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Refused("score-invalid", null));
        TitlesOf(B2List);
        var first = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        // the list scrolls away and back
        TitlesOf(PeopleList);
        await run.HandleAsync(Screenshot(PeopleList), CancellationToken.None);
        TitlesOf(B2List);
        var back = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        Assert.Equal(3, first.PerfectGames.OfType<PerfectGameOutcome.NotRecorded>().Count());
        Assert.Empty(back.PerfectGames);
        _site.Verify(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == "Awakening"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Never);
        Assert.Equal(0, run.Tally.NotSent);
    }

    [Fact]
    public async Task APerfectGameLostToTheNetworkIsSentWhenItsRowNextSettles()
    {
        var run = TesterRun(("B2", 7, 1_000_000));
        TitlesOf(B2List);
        _site.SetupSequence(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == "BANYA HIPHOP REMIX" && p.Level == 8), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Failed(null, "no network"))
            .ReturnsAsync(new PostOutcome.Recorded(1, "Rise"));
        await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        // the list scrolls away and back
        TitlesOf(PeopleList);
        await run.HandleAsync(Screenshot(PeopleList), CancellationToken.None);
        TitlesOf(B2List);
        var back = await run.HandleAsync(Screenshot(B2List), CancellationToken.None);

        // its S6 went up the first time and is already there; the rest of B2's rows are done
        Assert.Equal([("BANYA HIPHOP REMIX", 8)], SentOf(back));
        Assert.Contains(new PerfectGameOutcome.AlreadyThere("BANYA HIPHOP REMIX", ChartType.Single, 6), back.PerfectGames);
        _site.Verify(s => s.PostAsync(It.Is<ObservedPlay>(p => p.SongName == "BANYA HIPHOP REMIX" && p.Level == 8), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

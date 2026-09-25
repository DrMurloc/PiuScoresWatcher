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

    private static readonly Guid AragamiS19 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MorrighanS20 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VacuumCleanerS20 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 19, 31, 18, TimeSpan.FromHours(-4));

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
        return new CapturedFrame(FixtureScreens.Load(fixture), CaptureSource.SteamScreenshot, _clock.Now, "shot.jpg");
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
    private async Task<BulkOutcome> AgainAsync(BulkCaptureRun run, string fixture, TimeSpan elapsed)
    {
        _clock.Advance(elapsed);
        return await run.HandleAsync(Window(fixture), CancellationToken.None);
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
}

using Moq;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.CaptureTests;

/// <summary>
///     Real fixture screens through the real detector and reader; the site, the title reader, the
///     failed-screen store and the notifier are doubles. What the pipeline posts, keeps, tells the
///     player, and waits for.
/// </summary>
public sealed class CapturePipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 20, 13, 28, TimeSpan.FromHours(-4));

    private readonly Mock<IPlaysClient> _site = new();
    private readonly Mock<ITitleReader> _titles = new();
    private readonly Mock<IFailedScreenStore> _failed = new();
    private readonly Mock<INotifier> _notifier = new();
    private readonly Mock<ISongCatalogs> _catalogs = new();
    private readonly FakeClock _clock = FakeClock.At(Now);

    public CapturePipelineTests()
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<PixelRect>(), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of("Morrighan"));
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Recorded(1, "Rise"));
        _failed.Setup(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()))
            .Returns(@"C:\failed\frame.png");
    }

    private CapturePipeline Pipeline()
    {
        return new CapturePipeline(new ResultScreenDetector(), new ResultScreenReader(), _titles.Object, _site.Object,
            new Deduplicator(_clock), _failed.Object, _notifier.Object, _catalogs.Object);
    }

    private static CapturedFrame Frame(string fixture, CaptureSource source = CaptureSource.GameWindow, double seconds = 0)
    {
        return new CapturedFrame(FixtureScreens.Load(fixture), source, Now.AddSeconds(seconds), null);
    }

    /// <summary>Morrighan with its accuracy ("94.93%") pasted over its perfect count, where Warm Up's layout puts both (Layouts.DanceGrade).</summary>
    private static CapturedFrame Misread(double seconds = 0, CaptureSource source = CaptureSource.GameWindow)
    {
        var image = FixtureScreens.LoadWithCopy("20260921201328",
            FractionRect.At1080p(1690, 779, 1850, 827), FractionRect.At1080p(1690, 191, 1850, 239));
        return new CapturedFrame(image, source, Now.AddSeconds(seconds), null);
    }

    /// <summary>Morrighan with its goods (13) pasted over its misses (14): every digit reads, and the numbers no longer agree.</summary>
    private static CapturedFrame Disagreeing(double seconds = 0)
    {
        var image = FixtureScreens.LoadWithCopy("20260921201328",
            FractionRect.At1080p(1690, 386, 1850, 434), FractionRect.At1080p(1690, 582, 1850, 630));
        return new CapturedFrame(image, CaptureSource.GameWindow, Now.AddSeconds(seconds), null);
    }

    private void NothingKept()
    {
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), It.IsAny<KeptBecause>(), It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Never);
        _notifier.Verify(n => n.Notify(It.IsAny<WatcherNotice.Unreadable>()), Times.Never);
    }

    private void TitleIs(params string[] reads)
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<PixelRect>(), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of(reads));
    }

    [Fact]
    public async Task AResultScreenIsPostedOnceWithTheTitleAndTheFrameTime()
    {
        var outcome = await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        var posted = Assert.IsType<FrameOutcome.Posted>(outcome);
        Assert.Equal("Morrighan", posted.Play.SongName);
        Assert.Equal(945403, posted.Play.Score);
        Assert.Equal(Now, posted.Play.PlayedAt);
        _site.Verify(s => s.PostAsync(It.Is<ObservedPlay>(p => p.Mix == RiseMix.Rise && p.Level == 20), "watcher-grab", It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.Notify(It.IsAny<WatcherNotice.Recorded>()), Times.Once);
    }

    [Fact]
    public async Task ATitleTheOcrSlippedOnIsPostedInTheCatalogsSpelling()
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<PixelRect>(), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of("Morrlghan"));
        _catalogs.Setup(c => c.For(RiseMix.Rise))
            .Returns(new SongCatalog([new CatalogChart(Guid.NewGuid(), "Morrighan", ChartType.Single, 20)]));

        var posted = Assert.IsType<FrameOutcome.Posted>(await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None));

        Assert.Equal("Morrighan", posted.Play.SongName);
    }

    [Fact]
    public async Task TheSameScreenSeenAgainIsADuplicate()
    {
        var pipeline = Pipeline();
        await pipeline.HandleAsync(Frame("20260921201328"), CancellationToken.None);

        var again = await pipeline.HandleAsync(Frame("20260921201328", CaptureSource.SteamScreenshot), CancellationToken.None);

        Assert.IsType<FrameOutcome.Duplicate>(again);
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFrameThatIsNotAResultScreenIsIgnored()
    {
        Assert.IsType<FrameOutcome.NotAResult>(await Pipeline().HandleAsync(Frame("20260922184847"), CancellationToken.None));
        _site.VerifyNoOtherCalls();
        _failed.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AChallengeAggregateIsNotAPlay()
    {
        Assert.IsType<FrameOutcome.NotAPlay>(await Pipeline().HandleAsync(Frame("20260921205242"), CancellationToken.None));
        _site.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AWindowFrameWhoseNumbersHaveNotLandedWaitsForTheNext()
    {
        Assert.IsType<FrameOutcome.NotYet>(await Pipeline().HandleAsync(Frame("20260922191729"), CancellationToken.None));
        _failed.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AWindowFrameCaughtMidCountWaitsForTheNext()
    {
        Assert.IsType<FrameOutcome.NotYet>(await Pipeline().HandleAsync(Frame("20260922192124"), CancellationToken.None));
        _site.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AScreenshotCaughtMidCountIsKeptBecauseNoNextFrameIsComing()
    {
        var outcome = await Pipeline().HandleAsync(Frame("20260922192124", CaptureSource.SteamScreenshot), CancellationToken.None);

        var kept = Assert.IsType<FrameOutcome.Kept>(outcome);
        Assert.Equal(@"C:\failed\frame.png", kept.SavedTo);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersDisagree, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
        _notifier.Verify(n => n.Notify(It.IsAny<WatcherNotice.Unreadable>()), Times.Once);
    }

    [Fact]
    public async Task AScreenWhoseTitleCannotBeReadIsKept()
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<PixelRect>(), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of());

        Assert.IsType<FrameOutcome.Kept>(await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None));
        _site.VerifyNoOtherCalls();
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TitleUnreadable, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task APlayARejectedTokenCostIsReportedWithThePlay()
    {
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Unauthorized());

        var outcome = await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        Assert.IsType<PostOutcome.Unauthorized>(Assert.IsType<FrameOutcome.Posted>(outcome).Outcome);
        _notifier.Verify(n => n.Notify(It.Is<WatcherNotice.NotRecorded>(x => x.Outcome is PostOutcome.Unauthorized && x.Play.SongName == "Morrighan")), Times.Once);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.TokenRejected, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task ASongTheSiteDoesNotKnowIsReportedNotRecorded()
    {
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.SongUnknown("Play 0: no chart matches on Rise."));

        await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        _notifier.Verify(n => n.Notify(It.Is<WatcherNotice.NotRecorded>(x => x.Outcome is PostOutcome.SongUnknown && x.SavedTo == @"C:\failed\frame.png")), Times.Once);
    }

    [Fact]
    public async Task APlayTheSiteCouldNotBeReachedForIsKeptNotLost()
    {
        _site.Setup(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostOutcome.Failed(null, "the site is unreachable"));

        await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.Unreachable, It.Is<string>(r => r.Contains("unreachable")), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task AWindowResultWhoseNumbersNeverAgreeIsKeptOnceTheyHaveStoodStill()
    {
        // D54: the Arcade Station's first 5 read as a 6 stayed "not yet" in the window for as long as it was up
        var pipeline = Pipeline();
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Frame("20260922192124"), CancellationToken.None));
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Frame("20260922192124", seconds: 2), CancellationToken.None));

        var outcome = await pipeline.HandleAsync(Frame("20260922192124", seconds: 3), CancellationToken.None);

        Assert.IsType<FrameOutcome.Kept>(outcome);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersDisagree, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
        _notifier.Verify(n => n.Notify(It.IsAny<WatcherNotice.Unreadable>()), Times.Once);
        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Frame("20260922192124", seconds: 10), CancellationToken.None));
    }

    [Fact]
    public async Task AWindowResultThatLeavesWithoutEverAgreeingIsKept()
    {
        // a still screen sends one frame; the next thing the window shows is the song wheel
        var pipeline = Pipeline();
        await pipeline.HandleAsync(Frame("20260922192124"), CancellationToken.None);

        var outcome = await pipeline.HandleAsync(Frame("20260922184847", seconds: 1), CancellationToken.None);

        Assert.IsType<FrameOutcome.NotAResult>(outcome);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersDisagree, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task AMisreadFrameBeforeTheScreenReadsRightIsDropped()
    {
        // owner, 2026-09-24: the first frame of a visit that adds up speaks for the screen
        var pipeline = Pipeline();
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Misread(0), CancellationToken.None));
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Disagreeing(1), CancellationToken.None));

        Assert.IsType<FrameOutcome.Posted>(await pipeline.HandleAsync(Frame("20260921201328", seconds: 2), CancellationToken.None));
        await pipeline.HandleAsync(Frame("20260922184847", seconds: 3), CancellationToken.None);

        NothingKept();
    }

    [Fact]
    public async Task AMisreadFrameAfterTheScreenReadRightIsDropped()
    {
        var pipeline = Pipeline();
        Assert.IsType<FrameOutcome.Posted>(await pipeline.HandleAsync(Frame("20260921201328"), CancellationToken.None));

        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Disagreeing(1), CancellationToken.None));
        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Misread(2), CancellationToken.None));
        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Frame("20260921201328", seconds: 3), CancellationToken.None));
        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Disagreeing(30), CancellationToken.None));
        await pipeline.HandleAsync(Frame("20260922184847", seconds: 31), CancellationToken.None);

        NothingKept();
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUnsettledResultItsScreenshotAlreadyKeptIsNotKeptTwice()
    {
        var pipeline = Pipeline();
        await pipeline.HandleAsync(Frame("20260922192124"), CancellationToken.None);
        await pipeline.HandleAsync(Frame("20260922192124", CaptureSource.SteamScreenshot, 1), CancellationToken.None);

        await pipeline.HandleAsync(Frame("20260922184847", seconds: 2), CancellationToken.None);

        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersDisagree, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task ACountThatReadsWithAPercentSignIsKeptForReviewNotAnError()
    {
        var outcome = await Pipeline().HandleAsync(Misread(source: CaptureSource.SteamScreenshot), CancellationToken.None);

        Assert.IsType<FrameOutcome.Kept>(outcome);
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersUnreadable, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task AWindowResultThatCannotBeReadIsKeptOnceWhileItStaysUp()
    {
        // the Arcade Station's video behind the numbers changes every frame; one screen is one review
        var pipeline = Pipeline();
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Misread(0), CancellationToken.None));
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Misread(2), CancellationToken.None));
        Assert.IsType<FrameOutcome.Kept>(await pipeline.HandleAsync(Misread(3), CancellationToken.None));
        Assert.IsType<FrameOutcome.Duplicate>(await pipeline.HandleAsync(Misread(4), CancellationToken.None));
        await pipeline.HandleAsync(Frame("20260922184847", seconds: 5), CancellationToken.None);

        // the next visit that never reads is kept when the screen goes away
        Assert.IsType<FrameOutcome.NotYet>(await pipeline.HandleAsync(Misread(60), CancellationToken.None));
        await pipeline.HandleAsync(Frame("20260922184847", seconds: 61), CancellationToken.None);

        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.NumbersUnreadable, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Exactly(2));
        _notifier.Verify(n => n.Notify(It.IsAny<WatcherNotice.Unreadable>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AResultLeftUpPastTheDedupeWindowIsNotPostedAgain()
    {
        // the screen stays up as long as the player leaves it (D11)
        var pipeline = Pipeline();
        Assert.IsType<FrameOutcome.Posted>(await pipeline.HandleAsync(Frame("20260921201328"), CancellationToken.None));

        _clock.Advance(TimeSpan.FromMinutes(11));
        var outcome = await pipeline.HandleAsync(Frame("20260921201328", seconds: 660), CancellationToken.None);

        Assert.IsType<FrameOutcome.Duplicate>(outcome);
        _site.Verify(s => s.PostAsync(It.IsAny<ObservedPlay>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayOnePointAboveTheFormulaIsPosted()
    {
        // D57: VECTOR's judgments make 901,012.99 and the screen prints 901,013; PIU Scores allows the point too
        TitleIs("VECTOR");

        var posted = Assert.IsType<FrameOutcome.Posted>(await Pipeline().HandleAsync(Frame("20260923223001"), CancellationToken.None));

        Assert.Equal((901013, ChartType.HalfDouble, 15), (posted.Play.Score, posted.Play.ChartType, posted.Play.Level));
    }

    [Fact]
    public async Task ALaterAttemptThatNamesAChartIsTheOnePosted()
    {
        TitleIs("Morrlqhan Remix", "Morrighan");
        _catalogs.Setup(c => c.For(RiseMix.Rise))
            .Returns(new SongCatalog([new CatalogChart(Guid.NewGuid(), "Morrighan", ChartType.Single, 20)]));

        var posted = Assert.IsType<FrameOutcome.Posted>(await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None));

        Assert.Equal("Morrighan", posted.Play.SongName);
    }

    [Fact]
    public async Task WithoutAChartListTheFirstReadingStands()
    {
        TitleIs("Morrlghan", "Morrighan");

        var posted = Assert.IsType<FrameOutcome.Posted>(await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None));

        Assert.Equal("Morrlghan", posted.Play.SongName);
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.IsAny<PixelRect>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnArcadePlayIsPostedAtTheLevelItsNotesAddUpTo()
    {
        // D56: 8 6's judgments add up to 550, which only its S16 has in this list
        TitleIs("86");
        _catalogs.Setup(c => c.For(RiseMix.RiseArcade)).Returns(new SongCatalog(
        [
            new CatalogChart(Guid.NewGuid(), "8 6", ChartType.Single, 12, 700),
            new CatalogChart(Guid.NewGuid(), "8 6", ChartType.Single, 16, 550)
        ]));

        var posted = Assert.IsType<FrameOutcome.Posted>(await Pipeline().HandleAsync(Frame("20260923224312"), CancellationToken.None));

        Assert.Equal(("8 6", 16), (posted.Play.SongName, posted.Play.Level));
    }

    [Fact]
    public async Task AnArcadePlayNoChartOfTheSongAddsUpToIsKept()
    {
        TitleIs("86");
        _catalogs.Setup(c => c.For(RiseMix.RiseArcade))
            .Returns(new SongCatalog([new CatalogChart(Guid.NewGuid(), "8 6", ChartType.Single, 12, 551)]));

        Assert.IsType<FrameOutcome.Kept>(await Pipeline().HandleAsync(Frame("20260923224312"), CancellationToken.None));
        _site.VerifyNoOtherCalls();
        _failed.Verify(f => f.Save(It.IsAny<CapturedFrame>(), KeptBecause.ChartDisagrees, It.IsAny<string>(), It.IsAny<ResultScreenReading?>()), Times.Once);
    }

    [Fact]
    public async Task ARecordedPlayIsNotKept()
    {
        await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        _failed.VerifyNoOtherCalls();
    }
}

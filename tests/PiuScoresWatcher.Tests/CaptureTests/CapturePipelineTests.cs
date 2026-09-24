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
            .ReturnsAsync("Morrighan");
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

    private static CapturedFrame Frame(string fixture, CaptureSource source = CaptureSource.GameWindow)
    {
        return new CapturedFrame(FixtureScreens.Load(fixture), source, Now, null);
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
            .ReturnsAsync("Morrlghan");
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
            .ReturnsAsync((string?)null);

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
    public async Task ARecordedPlayIsNotKept()
    {
        await Pipeline().HandleAsync(Frame("20260921201328"), CancellationToken.None);

        _failed.VerifyNoOtherCalls();
    }
}

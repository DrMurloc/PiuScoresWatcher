using Moq;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Sessions;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.SessionTests;

/// <summary>
///     The watcher's half of a session (watcher.md D74–D82): what is open, what is owed, and what reaches PIU Scores —
///     against a stub site and a store fake that remembers every save, so a restart is a new keeper over the same store.
/// </summary>
public sealed class SessionKeeperTests
{
    private static readonly DateTimeOffset Evening = new(2026, 9, 26, 19, 4, 0, TimeSpan.FromHours(-4));

    private readonly FakeClock _clock = FakeClock.At(Evening);
    private readonly Mock<IPlaysClient> _site = new();
    private readonly MemoryStore _store = new();

    public SessionKeeperTests()
    {
        SiteAnswers(new CloseOutcome.Closed());
    }

    private SessionKeeper Keeper()
    {
        return new SessionKeeper(_site.Object, _store, _clock);
    }

    private void SiteAnswers(CloseOutcome outcome)
    {
        _site.Setup(site => site.CloseSittingsAsync(It.IsAny<RiseMix>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
    }

    private void Closed(RiseMix mix, int times)
    {
        _site.Verify(site => site.CloseSittingsAsync(mix, It.IsAny<CancellationToken>()), Times.Exactly(times));
    }

    [Fact]
    public async Task WithNothingPlayedThereIsNothingToEnd()
    {
        var keeper = Keeper();

        keeper.End();
        var sent = await keeper.SendOwedAsync(CancellationToken.None);

        Assert.False(keeper.IsOpen);
        Assert.Empty(sent);
        _site.Verify(site => site.CloseSittingsAsync(It.IsAny<RiseMix>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APlayOpensItsStationsSessionAndEndingItClosesThatStationOnce()
    {
        var keeper = Keeper();

        keeper.Recorded(RiseMix.Rise);
        Assert.True(keeper.IsOpen);
        keeper.End();
        var sent = await keeper.SendOwedAsync(CancellationToken.None);

        Assert.Equal(RiseMix.Rise, Assert.Single(sent).Mix);
        Closed(RiseMix.Rise, 1);
        Closed(RiseMix.RiseArcade, 0);
        Assert.False(keeper.IsOpen);
        Assert.False(keeper.HasOwed);
    }

    [Fact]
    public async Task ANightOnBothStationsEndsBoth()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        keeper.Recorded(RiseMix.RiseArcade);
        keeper.Recorded(RiseMix.Rise);

        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);

        Closed(RiseMix.Rise, 1);
        Closed(RiseMix.RiseArcade, 1);
    }

    [Fact]
    public async Task ACloseIsWrittenDownBeforeItIsSent()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        SessionState? whenSent = null;
        _site.Setup(site => site.CloseSittingsAsync(RiseMix.Rise, It.IsAny<CancellationToken>()))
            .Callback(() => whenSent = _store.Saved)
            .ReturnsAsync(new CloseOutcome.Closed());

        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);

        Assert.Equal(RiseMix.Rise, Assert.Single(whenSent!.Owed).Mix);
        Assert.Empty(whenSent.Open);
        Assert.Empty(_store.Saved.Owed);
    }

    [Fact]
    public async Task ACloseThatDidNotGetThroughIsSentAgainUntilItLands()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        SiteAnswers(new CloseOutcome.Failed(null, "the site is unreachable"));

        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);
        Assert.True(keeper.HasOwed);
        SiteAnswers(new CloseOutcome.Closed());
        _clock.Advance(TimeSpan.FromSeconds(30));
        await keeper.SendOwedAsync(CancellationToken.None);

        Closed(RiseMix.Rise, 2);
        Assert.False(keeper.HasOwed);
    }

    [Fact]
    public async Task AFinalAnswerIsNeverSentAgain()
    {
        foreach (var final in new CloseOutcome[] { new CloseOutcome.Refused("mix-required", null), new CloseOutcome.NotOffered() })
        {
            var keeper = new SessionKeeper(_site.Object, new MemoryStore(), _clock);
            keeper.Recorded(RiseMix.Rise);
            SiteAnswers(final);

            keeper.End();
            await keeper.SendOwedAsync(CancellationToken.None);

            Assert.False(keeper.HasOwed);
        }
    }

    [Fact]
    public async Task ACloseStillOwedFourHoursAfterItsSessionsLastPlayIsDroppedUnsent()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        SiteAnswers(new CloseOutcome.Failed(503, "down"));
        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);

        _clock.Advance(SessionKeeper.SiteFallback);
        var sent = await keeper.SendOwedAsync(CancellationToken.None);

        Assert.Null(Assert.Single(sent).Outcome);
        Closed(RiseMix.Rise, 1);
        Assert.False(keeper.HasOwed);
    }

    [Fact]
    public async Task APlayOnAStationOwedACloseMakesTheCloseMoot()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        SiteAnswers(new CloseOutcome.Failed(null, "the site is unreachable"));
        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);

        keeper.Recorded(RiseMix.Rise);

        Assert.False(keeper.HasOwed);
        Assert.True(keeper.IsOpen);
    }

    [Fact]
    public void QuietCountsFromTheLastPlayOnEitherStation()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        _clock.Advance(TimeSpan.FromMinutes(10));
        keeper.Recorded(RiseMix.RiseArcade);
        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.False(keeper.QuietFor(TimeSpan.FromMinutes(15)));
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(keeper.QuietFor(TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void NothingOpenIsNeverQuiet()
    {
        Assert.False(Keeper().QuietFor(TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task ASessionTheSiteHasEndedByItselfLapsesAndIsNotClosedLater()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        _clock.Advance(TimeSpan.FromHours(1));
        keeper.Recorded(RiseMix.RiseArcade);

        _clock.Advance(TimeSpan.FromHours(3));
        Assert.True(keeper.Lapse());
        Assert.False(keeper.Lapse());
        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);

        Closed(RiseMix.Rise, 0);
        Closed(RiseMix.RiseArcade, 1);
    }

    [Fact]
    public async Task ARestartPicksUpWhereTheLastRunLeftOff()
    {
        var before = Keeper();
        before.Recorded(RiseMix.RiseArcade);
        before.Recorded(RiseMix.Rise);
        SiteAnswers(new CloseOutcome.Unauthorized());
        before.End();
        await before.SendOwedAsync(CancellationToken.None);
        before.Recorded(RiseMix.RiseArcade);

        _clock.Advance(TimeSpan.FromMinutes(20));
        var after = Keeper();
        SiteAnswers(new CloseOutcome.Closed());

        Assert.True(after.IsOpen);
        Assert.True(after.HasOwed);
        await after.SendOwedAsync(CancellationToken.None);
        Closed(RiseMix.Rise, 2);
        Assert.False(after.HasOwed);
        Assert.Equal(Evening, after.LastPlayAt);
    }

    [Fact]
    public async Task ForgettingDropsWhatWasOpenAndWhatWasOwed()
    {
        var keeper = Keeper();
        keeper.Recorded(RiseMix.Rise);
        SiteAnswers(new CloseOutcome.Failed(null, "the site is unreachable"));
        keeper.End();
        await keeper.SendOwedAsync(CancellationToken.None);
        keeper.Recorded(RiseMix.RiseArcade);

        keeper.Forget();

        Assert.False(keeper.IsOpen);
        Assert.False(keeper.HasOwed);
        Assert.Equal(SessionState.Empty, _store.Saved);
    }

    /// <summary>A store that keeps the last state saved, as the file would.</summary>
    private sealed class MemoryStore : ISessionStore
    {
        public SessionState Saved { get; private set; } = SessionState.Empty;

        public SessionState Load() => Saved;

        public void Save(SessionState state) => Saved = state;
    }
}

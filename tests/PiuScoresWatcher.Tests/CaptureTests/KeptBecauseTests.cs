using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.Tests.CaptureTests;

public sealed class KeptBecauseTests
{
    [Theory]
    [InlineData(KeptBecause.ListUnreadable, true)]
    [InlineData(KeptBecause.GradeDisagrees, true)]
    [InlineData(KeptBecause.TitleUnmatched, true)]
    [InlineData(KeptBecause.NumbersUnreadable, false)]
    [InlineData(KeptBecause.TitleUnreadable, false)]
    [InlineData(KeptBecause.Refused, false)]
    public void ASongListKeptDuringABulkCaptureIsToldApartFromAResultScreen(KeptBecause because, bool isSongList)
    {
        Assert.Equal(isSongList, because.IsSongList());
    }

    [Theory]
    [InlineData(KeptBecause.NumbersUnreadable, false)]
    [InlineData(KeptBecause.NumbersNotShown, false)]
    [InlineData(KeptBecause.NumbersDisagree, false)]
    [InlineData(KeptBecause.TitleUnreadable, false)]
    [InlineData(KeptBecause.ListUnreadable, false)]
    [InlineData(KeptBecause.GradeDisagrees, false)]
    [InlineData(KeptBecause.TitleUnmatched, false)]
    [InlineData(KeptBecause.Refused, true)]
    [InlineData(KeptBecause.SongUnknown, true)]
    [InlineData(KeptBecause.TokenRejected, true)]
    [InlineData(KeptBecause.NotConnected, true)]
    [InlineData(KeptBecause.RateLimited, true)]
    [InlineData(KeptBecause.Unreachable, true)]
    public void AScreenThatCouldNotBeReadIsToldApartFromAPlayTheSiteDidNotRecord(KeptBecause because, bool wasRead)
    {
        Assert.Equal(wasRead, because.WasRead());
    }

    [Fact]
    public void EveryWayThePostCanFailHasItsOwnReason()
    {
        Assert.Equal(KeptBecause.Refused, Kept.Because(new PostOutcome.Refused("judgments-do-not-reconcile", null)));
        Assert.Equal(KeptBecause.SongUnknown, Kept.Because(new PostOutcome.SongUnknown(null)));
        Assert.Equal(KeptBecause.TokenRejected, Kept.Because(new PostOutcome.Unauthorized()));
        Assert.Equal(KeptBecause.NotConnected, Kept.Because(new PostOutcome.NotConnected()));
        Assert.Equal(KeptBecause.RateLimited, Kept.Because(new PostOutcome.RateLimited(null)));
        Assert.Equal(KeptBecause.Unreachable, Kept.Because(new PostOutcome.Failed(503, "unavailable")));
    }

    [Fact]
    public void ARecordedPlayHasNoReasonToBeKept()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Kept.Because(new PostOutcome.Recorded(1, "Rise")));
    }
}

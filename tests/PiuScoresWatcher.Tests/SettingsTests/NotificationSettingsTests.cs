using System.Text.Json;
using System.Text.Json.Serialization;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.Tests.SettingsTests;

public sealed class NotificationSettingsTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void EveryKindIsOnByDefault()
    {
        var defaults = NotificationSettings.Default;

        Assert.True(defaults.Allows(new WatcherNotice.TokenRejected()));
        Assert.True(defaults.Allows(new WatcherNotice.Updated("0.2.0")));
        Assert.True(defaults.Allows(new WatcherNotice.Unreadable("why", "where")));
    }

    [Fact]
    public void TheMasterSwitchSilencesEveryKind()
    {
        var off = NotificationSettings.Default with { Enabled = false };

        Assert.False(off.Allows(new WatcherNotice.TokenRejected()));
        Assert.False(off.Allows(new WatcherNotice.Updated("0.2.0")));
        Assert.False(off.Allows(new WatcherNotice.Unreadable("why", "where")));
    }

    [Fact]
    public void OneKindCanBeSilencedAlone()
    {
        var noUpdates = NotificationSettings.Default with { Updated = false };

        Assert.False(noUpdates.Allows(new WatcherNotice.Updated("0.2.0")));
        Assert.True(noUpdates.Allows(new WatcherNotice.TokenRejected()));
    }

    [Fact]
    public void APlayLostToTheTokenFollowsTheTokenSwitchNotTheNotRecordedOne()
    {
        var play = new ObservedPlay(RiseMix.Rise, "Morrighan", ChartType.Single, 20, Judgments.From(900, 50, 10, 5, 3), 700, 945403, false,
            DateTimeOffset.UnixEpoch);
        var tokenOff = NotificationSettings.Default with { TokenRejected = false };

        Assert.False(tokenOff.Allows(new WatcherNotice.NotRecorded(play, new PostOutcome.Unauthorized(), "where")));
        Assert.False(tokenOff.Allows(new WatcherNotice.NotRecorded(play, new PostOutcome.NotConnected(), "where")));
        Assert.True(tokenOff.Allows(new WatcherNotice.NotRecorded(play, new PostOutcome.SongUnknown(null), "where")));
    }

    [Fact]
    public void ASettingsFileFromBeforeNotificationsExistedMeansAllOn()
    {
        var settings = JsonSerializer.Deserialize<WatcherSettings>(
            """{"Mode":"Both","StartWithWindows":true,"SteamScreenshotsFolder":null}""", Options)!;

        Assert.Null(settings.Notifications);
        Assert.Equal(NotificationSettings.Default, settings.EffectiveNotifications);
        Assert.Null(settings.LastSeenVersion);
    }

    [Fact]
    public void TheFileHoldsOnlyWhatThePlayerChose()
    {
        var json = JsonSerializer.Serialize(WatcherSettings.Default, Options);

        Assert.DoesNotContain(nameof(WatcherSettings.EffectiveNotifications), json, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchedOffKindsSurviveARoundTrip()
    {
        var chosen = WatcherSettings.Default with { Notifications = NotificationSettings.Default with { Recorded = false } };

        var back = JsonSerializer.Deserialize<WatcherSettings>(JsonSerializer.Serialize(chosen, Options), Options)!;

        Assert.False(back.EffectiveNotifications.Recorded);
        Assert.True(back.EffectiveNotifications.Unreadable);
    }

    [Fact]
    public void AFileFromBeforeBulkCaptureKeepsItsSoundsAndItsSummaryOn()
    {
        var settings = JsonSerializer.Deserialize<WatcherSettings>(
            """{"Mode":"Both","StartWithWindows":true,"SteamScreenshotsFolder":null,"Notifications":{"Enabled":true,"Recorded":false,"NotRecorded":true,"Unreadable":true,"TokenRejected":true,"Updated":true}}""",
            Options)!;

        Assert.True(settings.BulkCaptureSounds);
        Assert.True(settings.EffectiveNotifications.BulkCaptureFinished);
        Assert.False(settings.EffectiveNotifications.Recorded);
    }

    [Fact]
    public void TheBulkCaptureSummaryHasItsOwnSwitch()
    {
        var summary = new WatcherNotice.BulkCaptureFinished(new BulkTally(3, 1, 0, 0));

        Assert.True(NotificationSettings.Default.Allows(summary));
        Assert.False((NotificationSettings.Default with { BulkCaptureFinished = false }).Allows(summary));
        Assert.False((NotificationSettings.Default with { Enabled = false }).Allows(summary));
    }
}

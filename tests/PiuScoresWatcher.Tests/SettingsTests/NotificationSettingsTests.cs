using System.Text.Json;
using System.Text.Json.Serialization;
using PiuScoresWatcher.Core.Capture;
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
    public void ASettingsFileFromBeforeNotificationsExistedMeansAllOn()
    {
        var settings = JsonSerializer.Deserialize<WatcherSettings>(
            """{"Mode":"Both","StartWithWindows":true,"SteamScreenshotsFolder":null}""", Options)!;

        Assert.Null(settings.Notifications);
        Assert.Equal(NotificationSettings.Default, settings.EffectiveNotifications);
        Assert.Null(settings.LastSeenVersion);
    }

    [Fact]
    public void SwitchedOffKindsSurviveARoundTrip()
    {
        var chosen = WatcherSettings.Default with { Notifications = NotificationSettings.Default with { Recorded = false } };

        var back = JsonSerializer.Deserialize<WatcherSettings>(JsonSerializer.Serialize(chosen, Options), Options)!;

        Assert.False(back.EffectiveNotifications.Recorded);
        Assert.True(back.EffectiveNotifications.Unreadable);
    }
}

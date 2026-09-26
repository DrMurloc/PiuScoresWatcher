using System.Text.Json;
using System.Text.Json.Serialization;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.Tests.SettingsTests;

/// <summary>The two switches that end a session and the minutes (watcher.md D75, D78), as the settings file keeps them.</summary>
public sealed class SessionSettingsTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void ByDefaultOnlyClosingRiseEndsASession()
    {
        var defaults = SessionSettings.Default;

        Assert.True(defaults.WhenRiseCloses);
        Assert.False(defaults.AfterQuiet);
        Assert.Equal(TimeSpan.FromMinutes(15), defaults.Quiet);
        Assert.True(defaults.EndsSessions);
    }

    [Fact]
    public void WithBothSwitchesOffTheWatcherEndsNothing()
    {
        Assert.False(new SessionSettings(WhenRiseCloses: false, AfterQuiet: false).EndsSessions);
        Assert.True(new SessionSettings(WhenRiseCloses: false, AfterQuiet: true).EndsSessions);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(45, 45)]
    [InlineData(60, 60)]
    [InlineData(0, 15)]
    [InlineData(-5, 15)]
    [InlineData(7, 15)]
    [InlineData(600, 15)]
    public void OnlyTheMinutesOnOfferAreTaken(int written, int taken)
    {
        Assert.Equal(TimeSpan.FromMinutes(taken), new SessionSettings(QuietMinutes: written).Quiet);
    }

    [Fact]
    public void ASettingsFileFromBeforeTheSwitchesEndsSessionsWhenRiseCloses()
    {
        var settings = JsonSerializer.Deserialize<WatcherSettings>(
            """{"Mode":"Both","StartWithWindows":true,"SteamScreenshotsFolder":null,"PlaySounds":true}""", Options)!;

        Assert.Null(settings.Sessions);
        Assert.Equal(SessionSettings.Default, settings.EffectiveSessions);
    }

    [Fact]
    public void TheChosenSwitchesSurviveARoundTripAndOnlyTheyAreWritten()
    {
        var chosen = WatcherSettings.Default with { Sessions = new SessionSettings(WhenRiseCloses: false, AfterQuiet: true, QuietMinutes: 30) };

        var json = JsonSerializer.Serialize(chosen, Options);
        var back = JsonSerializer.Deserialize<WatcherSettings>(json, Options)!;

        Assert.Equal(chosen.Sessions, back.EffectiveSessions);
        Assert.DoesNotContain(nameof(WatcherSettings.EffectiveSessions), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(SessionSettings.EndsSessions), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(SessionSettings.EffectiveQuietMinutes), json, StringComparison.Ordinal);
    }

    [Fact]
    public void ASessionsEntryMissingAFieldTakesThatFieldsDefault()
    {
        var settings = JsonSerializer.Deserialize<WatcherSettings>(
            """{"Mode":"Both","StartWithWindows":true,"SteamScreenshotsFolder":null,"Sessions":{"AfterQuiet":true}}""", Options)!;

        Assert.Equal(new SessionSettings(WhenRiseCloses: true, AfterQuiet: true, QuietMinutes: 15), settings.EffectiveSessions);
    }
}

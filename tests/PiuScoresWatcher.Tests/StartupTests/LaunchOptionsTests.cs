using PiuScoresWatcher.Core.Exceptions;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.Tests.StartupTests;

public sealed class LaunchOptionsTests
{
    [Fact]
    public void NoSwitchesIsAPlayerLaunchAgainstProduction()
    {
        var options = LaunchOptions.Parse([]);

        Assert.Null(options.ReplayFile);
        Assert.False(options.DryRun);
        Assert.Null(options.BaseUrl);
        Assert.Equal(LaunchOptions.ProductionBaseUrl, options.EffectiveBaseUrl);
    }

    [Fact]
    public void ReplayTakesItsFileAndDryRunStandsAlone()
    {
        var options = LaunchOptions.Parse(["--replay", @"C:\shots\result.png", "--dry-run"]);

        Assert.Equal(@"C:\shots\result.png", options.ReplayFile);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void SwitchesAreCaseInsensitive()
    {
        Assert.True(LaunchOptions.Parse(["--Dry-Run"]).DryRun);
    }

    [Fact]
    public void TheBaseUrlSwitchPointsTheWatcherAtAnotherSite()
    {
        var options = LaunchOptions.Parse(["--base-url", "https://localhost:7148"]);

        Assert.Equal(new Uri("https://localhost:7148"), options.BaseUrl);
        Assert.Equal(new Uri("https://localhost:7148"), options.EffectiveBaseUrl);
    }

    [Fact]
    public void TheEnvironmentVariableFillsInWhenNoSwitchDoes()
    {
        var options = LaunchOptions.Parse([], "http://localhost:5000");

        Assert.Equal(new Uri("http://localhost:5000"), options.BaseUrl);
    }

    [Fact]
    public void TheSwitchOutranksTheEnvironmentVariable()
    {
        var options = LaunchOptions.Parse(["--base-url", "https://localhost:7148"], "http://localhost:5000");

        Assert.Equal(new Uri("https://localhost:7148"), options.BaseUrl);
    }

    [Fact]
    public void ABlankEnvironmentVariableIsNoOverride()
    {
        Assert.Null(LaunchOptions.Parse([], "   ").BaseUrl);
    }

    [Theory]
    [InlineData("--replay")]
    [InlineData("--base-url")]
    public void AValueSwitchWithoutItsValueIsRefused(string @switch)
    {
        Assert.Throws<InvalidLaunchOptionsException>(() => LaunchOptions.Parse([@switch]));
    }

    [Fact]
    public void AValueSwitchFollowedByAnotherSwitchIsRefused()
    {
        Assert.Throws<InvalidLaunchOptionsException>(() => LaunchOptions.Parse(["--replay", "--dry-run"]));
    }

    [Theory]
    [InlineData("localhost:7148")]
    [InlineData("ftp://piuscores.arroweclip.se/")]
    [InlineData("/relative")]
    public void ABaseUrlMustBeAnAbsoluteHttpUrl(string value)
    {
        Assert.Throws<InvalidLaunchOptionsException>(() => LaunchOptions.Parse(["--base-url", value]));
        Assert.Throws<InvalidLaunchOptionsException>(() => LaunchOptions.Parse([], value));
    }

    [Theory]
    [InlineData("-ToastActivated")]
    [InlineData("-Embedding")]
    public void TheSwitchesWindowsAddsOnANotificationClickAreNotRefused(string windowsSwitch)
    {
        Assert.Equal(LaunchOptions.None, LaunchOptions.Parse([windowsSwitch]));
    }

    [Fact]
    public void AnUnknownSwitchIsRefusedRatherThanIgnored()
    {
        var refusal = Assert.Throws<InvalidLaunchOptionsException>(() => LaunchOptions.Parse(["--overlay"]));

        Assert.Contains("--overlay", refusal.Message, StringComparison.Ordinal);
    }
}

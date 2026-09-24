using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.Tests.StartupTests;

/// <summary>A dev run against a local site keeps its own data and lock, so it never touches the installed copy (D44).</summary>
public sealed class SiteScopeTests
{
    private const string RoamingAppData = @"C:\Users\player\AppData\Roaming";

    [Theory]
    [InlineData("https://piuscores.arroweclip.se/")]
    [InlineData("https://PIUScores.ArroweClip.se")]
    [InlineData("http://piuscores.arroweclip.se:443/some/path")]
    public void ProductionKeepsTheWatchersOwnFolderAndLock(string site)
    {
        var scope = new SiteScope(new Uri(site));

        Assert.True(scope.IsProduction);
        Assert.Equal(Path.Combine(RoamingAppData, "PiuScoresWatcher"), scope.DataFolder(RoamingAppData));
        Assert.Equal("PiuScoresWatcher", scope.InstanceName);
    }

    [Fact]
    public void ALocalSiteKeepsAFolderAndALockOfItsOwn()
    {
        var scope = new SiteScope(new Uri("https://localhost:7144/"));

        Assert.False(scope.IsProduction);
        Assert.Equal(Path.Combine(RoamingAppData, "PiuScoresWatcher", "dev", "localhost-7144"), scope.DataFolder(RoamingAppData));
        Assert.Equal("PiuScoresWatcher.dev.localhost-7144", scope.InstanceName);
        Assert.Equal("localhost:7144", scope.Label);
    }

    [Fact]
    public void TwoLocalSitesDoNotShareAFolderOrALock()
    {
        var https = new SiteScope(new Uri("https://localhost:7144/"));
        var http = new SiteScope(new Uri("http://localhost:5144/"));

        Assert.NotEqual(https.DataFolder(RoamingAppData), http.DataFolder(RoamingAppData));
        Assert.NotEqual(https.InstanceName, http.InstanceName);
    }

    [Fact]
    public void AnAddressThatIsNoFileNameStillMakesOne()
    {
        var scope = new SiteScope(new Uri("https://[::1]:7144/"));

        var folder = Path.GetFileName(scope.DataFolder(RoamingAppData));
        Assert.DoesNotContain(folder, c => Path.GetInvalidFileNameChars().Contains(c) || c is ':' or '[' or ']');
        Assert.DoesNotContain('\\', scope.InstanceName);
    }

    [Fact]
    public void WithNoSiteNamedTheWatcherIsOnProduction()
    {
        Assert.True(LaunchOptions.Parse([]).Scope.IsProduction);
        Assert.Equal(SiteScope.Production, LaunchOptions.Parse([]).Scope);
    }

    [Fact]
    public void TheLaunchProfilesSiteIsNotProduction()
    {
        var scope = LaunchOptions.Parse([], "https://localhost:7144/").Scope;

        Assert.False(scope.IsProduction);
        Assert.Equal("localhost:7144", scope.Label);
    }
}

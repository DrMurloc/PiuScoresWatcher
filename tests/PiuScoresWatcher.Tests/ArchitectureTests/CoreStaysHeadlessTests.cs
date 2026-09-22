using System.Reflection;
using System.Runtime.Versioning;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.Tests.ArchitectureTests;

/// <summary>
///     Core is the recognizer, the checksum and the contracts, and it runs wherever the SDK does —
///     which is what makes a fixture screenshot a unit test. Windows enters only through App. Rules
///     here are added, never removed.
/// </summary>
public sealed class CoreStaysHeadlessTests
{
    private static readonly Assembly Core = typeof(LaunchOptions).Assembly;

    [Fact]
    public void CoreTargetsNoOperatingSystem()
    {
        // A net10.0-windows target stamps the assembly with the platform; plain net10.0 does not.
        Assert.Null(Core.GetCustomAttribute<SupportedOSPlatformAttribute>());
    }

    [Theory]
    [InlineData("PresentationFramework")]
    [InlineData("PresentationCore")]
    [InlineData("WindowsBase")]
    [InlineData("System.Windows.Forms")]
    [InlineData("Microsoft.Windows.SDK.NET")]
    [InlineData("H.NotifyIcon")]
    [InlineData("Velopack")]
    public void CoreReferencesNoUiOrWindowsAssembly(string forbidden)
    {
        Assert.DoesNotContain(Core.GetReferencedAssemblies(),
            reference => reference.Name!.StartsWith(forbidden, StringComparison.Ordinal));
    }
}

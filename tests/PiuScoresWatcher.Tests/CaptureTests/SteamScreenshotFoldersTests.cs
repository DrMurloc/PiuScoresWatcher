using PiuScoresWatcher.Core.Capture;

namespace PiuScoresWatcher.Tests.CaptureTests;

public sealed class SteamScreenshotFoldersTests
{
    [Fact]
    public void OneFolderPerSteamAccountThatLooksLikeAnAccount()
    {
        var folders = SteamScreenshotFolders.Resolve(@"C:\Steam", ["225097303", "12345678", "config", "ac"], null);

        Assert.Equal(
        [
            @"C:\Steam\userdata\225097303\760\remote\2756930\screenshots",
            @"C:\Steam\userdata\12345678\760\remote\2756930\screenshots"
        ], folders);
    }

    [Fact]
    public void AnOverrideFolderReplacesDetection()
    {
        Assert.Equal([@"D:\shots"], SteamScreenshotFolders.Resolve(@"C:\Steam", ["225097303"], @" D:\shots "));
    }

    [Fact]
    public void NoSteamMeansNoFolders()
    {
        Assert.Empty(SteamScreenshotFolders.Resolve(null, ["225097303"], null));
    }

    [Theory]
    [InlineData(@"C:\s\screenshots\20260922192124_1.jpg", true)]
    [InlineData(@"C:\s\screenshots\thumbnails\20260922192124_1.jpg", false)]
    [InlineData(@"C:\s\screenshots\20260922192124_1.png", false)]
    public void OnlyTheScreenshotItselfCounts(string path, bool expected)
    {
        Assert.Equal(expected, SteamScreenshotFolders.IsScreenshot(path));
    }
}

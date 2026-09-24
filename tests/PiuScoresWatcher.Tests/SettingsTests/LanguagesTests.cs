using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.Tests.SettingsTests;

public sealed class LanguagesTests
{
    [Fact]
    public void ThePickerListsTheSitesLanguagesLessMurlocInTheSitesOrder()
    {
        Assert.Equal(["en-US", "es-MX", "es-ES", "pt-BR", "ko-KR", "ja-JP", "fr-FR", "it-IT"], Languages.All);
    }

    [Theory]
    [InlineData("ko-KR", "ko-KR")]
    [InlineData("KO-kr", "ko-KR")]
    [InlineData("es-MX", "es-MX")]
    [InlineData("en-ZW", null)]
    [InlineData("de-DE", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AStoredChoiceIsASpokenLanguageInItsOwnSpellingOrMachineDefault(string? stored, string? expected)
    {
        Assert.Equal(expected, Languages.Normalize(stored));
    }

    [Theory]
    [InlineData("es-MX", "es-MX")]
    [InlineData("es-CL", "es-ES")]
    [InlineData("es-419", "es-ES")]
    [InlineData("es", "es-ES")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("ko", "ko-KR")]
    [InlineData("ja", "ja-JP")]
    [InlineData("it-CH", "it-IT")]
    [InlineData("en-GB", "en-US")]
    public void AWindowsLanguagePlacesTheWayTheSitePlacesABrowsers(string tag, string expected)
    {
        Assert.Equal(expected, Languages.Closest(tag));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("zh-Hans-CN")]
    [InlineData("  ")]
    public void ALanguageTheWatcherDoesNotSpeakPlacesNowhere(string tag)
    {
        Assert.Null(Languages.Closest(tag));
    }

    [Fact]
    public void EnglishFromZimbabweIsEnglishNotMurloc()
    {
        // Murloc is the site's joke locale on en-ZW; the watcher does not speak it (owner, 2026-09-24).
        Assert.Equal("en-US", Languages.Closest("en-ZW"));
    }

    [Fact]
    public void AChoiceBeatsWindows()
    {
        Assert.Equal("ja-JP", Languages.Resolve("ja-JP", ["ko-KR"]));
    }

    [Fact]
    public void MachineDefaultTakesTheFirstWindowsLanguageThatPlaces()
    {
        Assert.Equal("es-ES", Languages.Resolve(null, ["de-DE", "es-AR", "fr-FR"]));
    }

    [Fact]
    public void MachineDefaultIsEnglishWhenNothingPlaces()
    {
        Assert.Equal("en-US", Languages.Resolve(null, ["de-DE", "zh-Hans-CN"]));
        Assert.Equal("en-US", Languages.Resolve(null, []));
    }

    [Fact]
    public void AStoredLanguageTheWatcherNoLongerSpeaksFallsBackToMachineDefault()
    {
        Assert.Equal("ko-KR", Languages.Resolve("en-ZW", ["ko"]));
    }
}

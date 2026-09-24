using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Tests.CatalogTests;

/// <summary>What the OCR read, turned into the catalog's own spelling — or nothing, when it isn't sure (D49).</summary>
public sealed class SongCatalogTests
{
    private static readonly SongCatalog Catalog = new(
    [
        Chart("Destr0yer", ChartType.Single, 22),
        Chart("L (PIU Edit)", ChartType.Single, 21),
        Chart("Aragami", ChartType.Single, 19),
        Chart("Aragami", ChartType.HalfDouble, 20),
        Chart("Conflict -NOMA CONCEIVER REMIX-", ChartType.Single, 22),
        Chart("Nakakapagpabagabag", ChartType.Single, 20),
        Chart("Gamma", ChartType.Single, 19),
        Chart("Gamma Ray", ChartType.Single, 19),
        Chart("Gamme", ChartType.Single, 19)
    ]);

    private static CatalogChart Chart(string song, ChartType type, int level)
    {
        return new CatalogChart(Guid.NewGuid(), song, type, level);
    }

    [Theory]
    [InlineData("Destr0yer", 22, "Destr0yer")]
    [InlineData("DestrOyer", 22, "Destr0yer")]
    [InlineData("L (PlU Edit)", 21, "L (PIU Edit)")]
    [InlineData("l (piu edit)", 21, "L (PIU Edit)")]
    [InlineData("Conflict -NOMA CONCEIVER REMIX -", 22, "Conflict -NOMA CONCEIVER REMIX-")]
    public void ALetterTheFontConfusesStillFindsTheSong(string read, int level, string expected)
    {
        Assert.Equal(expected, Catalog.Match(read, ChartType.Single, level)?.SongName);
    }

    [Fact]
    public void AMisreadLetterOrTwoWinsWhenNothingElseIsAsClose()
    {
        Assert.Equal("Nakakapagpabagabag", Catalog.Match("Nakakapagpabagaboq", ChartType.Single, 20)?.SongName);
    }

    [Fact]
    public void OnlySongsWithTheLitChartAreCandidates()
    {
        Assert.Null(Catalog.Match("Aragami", ChartType.Single, 20));
        Assert.Equal("Aragami", Catalog.Match("Aragami", ChartType.HalfDouble, 20)?.SongName);
    }

    [Fact]
    public void AnExactNameBeatsALongerOne()
    {
        Assert.Equal("Gamma", Catalog.Match("Gamma", ChartType.Single, 19)?.SongName);
        Assert.Equal("Gamma Ray", Catalog.Match("Gamma Ray", ChartType.Single, 19)?.SongName);
    }

    [Fact]
    public void TwoSongsEquallyCloseMatchNeither()
    {
        // one letter from Gamma and one from Gamme: guessing would post the wrong song half the time
        Assert.Null(Catalog.Match("Gammo", ChartType.Single, 19));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Something Else Entirely")]
    public void ATitleThatNamesNoSongMatchesNothing(string read)
    {
        Assert.Null(Catalog.Match(read, ChartType.Single, 19));
    }

    [Fact]
    public void AKeyIgnoresCaseSpacingAndPunctuation()
    {
        Assert.Equal(SongCatalog.Key("Conflict -NOMA CONCEIVER REMIX-"), SongCatalog.Key("conflict noma conceiver remix"));
    }
}

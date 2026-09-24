using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Tests.CatalogTests;

/// <summary>
///     What the OCR read, turned into the catalog's own spelling — or nothing, when it isn't sure (D49) — and
///     where the catalog knows note counts, the chart the judgments add up to (D56).
/// </summary>
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
        Chart("Gamme", ChartType.Single, 19),
        Chart("K.O.A : Alice In Wonderworld", ChartType.Single, 17),
        Chart("Love Code", ChartType.Single, 17)
    ]);

    /// <summary>The Arcade Station's charts as the site lists them, note counts and all.</summary>
    private static readonly SongCatalog Arcade = new(
    [
        Chart("8 6", ChartType.Single, 12, 550),
        Chart("8 6", ChartType.Single, 16, 1500),
        Chart("8 6", ChartType.Single, 20, 2300),
        Chart("VANISH", ChartType.Single, 20, 1222),
        Chart("VANISH", ChartType.Single, 17, 1067),
        Chart("%X (Percent X)", ChartType.Single, 18, 1169),
        Chart("%X (Percent X)", ChartType.Single, 20, 1271),
        Chart("D", ChartType.Single, 12)
    ]);

    private static CatalogChart Chart(string song, ChartType type, int level, int? notes = null)
    {
        return new CatalogChart(Guid.NewGuid(), song, type, level, notes);
    }

    private static string? Named(CatalogMatch match)
    {
        return (match as CatalogMatch.Found)?.Chart.SongName;
    }

    [Theory]
    [InlineData("Destr0yer", 22, "Destr0yer")]
    [InlineData("DestrOyer", 22, "Destr0yer")]
    [InlineData("L (PlU Edit)", 21, "L (PIU Edit)")]
    [InlineData("l (piu edit)", 21, "L (PIU Edit)")]
    [InlineData("Conflict -NOMA CONCEIVER REMIX -", 22, "Conflict -NOMA CONCEIVER REMIX-")]
    public void ALetterTheFontConfusesStillFindsTheSong(string read, int level, string expected)
    {
        Assert.Equal(expected, Named(Catalog.Match(read, ChartType.Single, level)));
    }

    [Fact]
    public void AMisreadLetterOrTwoWinsWhenNothingElseIsAsClose()
    {
        Assert.Equal("Nakakapagpabagabag", Named(Catalog.Match("Nakakapagpabagaboq", ChartType.Single, 20)));
    }

    [Fact]
    public void ATitleReadWithAPieceMissingFindsTheOneSongItFits()
    {
        // the OCR turned "K.O.A :" into a bullet
        Assert.Equal("K.O.A : Alice In Wonderworld", Named(Catalog.Match("• Alice In Wonderworld", ChartType.Single, 17)));
    }

    [Fact]
    public void ATitleReadWithAPieceExtraFindsTheOneSongItFits()
    {
        // the OCR read %X as O/ox
        Assert.Equal("%X (Percent X)", Named(Arcade.Match("O/ox (Percent X)", ChartType.Single, 18)));
    }

    [Fact]
    public void APieceTooSmallOfTheTitleNamesNothing()
    {
        // "Love" is half of "Love Code": a reading that short could be any Love
        Assert.IsType<CatalogMatch.NotFound>(Catalog.Match("Love", ChartType.Single, 17));
    }

    [Fact]
    public void OnlySongsWithTheLitChartAreCandidates()
    {
        Assert.IsType<CatalogMatch.NotFound>(Catalog.Match("Aragami", ChartType.Single, 20));
        Assert.Equal("Aragami", Named(Catalog.Match("Aragami", ChartType.HalfDouble, 20)));
    }

    [Fact]
    public void AnExactNameBeatsALongerOne()
    {
        Assert.Equal("Gamma", Named(Catalog.Match("Gamma", ChartType.Single, 19)));
        Assert.Equal("Gamma Ray", Named(Catalog.Match("Gamma Ray", ChartType.Single, 19)));
    }

    [Fact]
    public void TwoSongsEquallyCloseMatchNeither()
    {
        // one letter from Gamma and one from Gamme: guessing would post the wrong song half the time
        Assert.IsType<CatalogMatch.NotFound>(Catalog.Match("Gammo", ChartType.Single, 19));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Something Else Entirely")]
    public void ATitleThatNamesNoSongMatchesNothing(string read)
    {
        Assert.IsType<CatalogMatch.NotFound>(Catalog.Match(read, ChartType.Single, 19));
    }

    [Fact]
    public void AKeyIgnoresCaseSpacingAndPunctuation()
    {
        Assert.Equal(SongCatalog.Key("Conflict -NOMA CONCEIVER REMIX-"), SongCatalog.Key("conflict noma conceiver remix"));
    }

    [Fact]
    public void AChartWhoseNotesAgreeWithTheJudgmentsIsTheOnePlayed()
    {
        var found = Assert.IsType<CatalogMatch.Found>(Arcade.Match("VANISH", ChartType.Single, 20, 1222));

        Assert.Equal(20, found.Chart.Level);
    }

    [Fact]
    public void AStepballReadAsALevelTheSongDoesNotHaveIsFoundByItsNotes()
    {
        // 8 6 has no S18: the F12 screenshot's stepball read 12 as 18, and 538 + 12 judgments are the S12's 550 notes
        var found = Assert.IsType<CatalogMatch.Found>(Arcade.Match("86", ChartType.Single, 18, 550));

        Assert.Equal(("8 6", 12), (found.Chart.SongName, found.Chart.Level));
    }

    [Fact]
    public void AStepballReadAsAnotherLevelOfTheSongIsCorrectedByItsNotes()
    {
        var found = Assert.IsType<CatalogMatch.Found>(Arcade.Match("8 6", ChartType.Single, 16, 550));

        Assert.Equal(12, found.Chart.Level);
    }

    [Fact]
    public void AGarbledTitleAtAMisreadLevelIsFoundByItsNotes()
    {
        // the window's frame read %X's 18 as 13, and the OCR read %X as O/ox
        var found = Assert.IsType<CatalogMatch.Found>(Arcade.Match("O/ox (Percent X)", ChartType.Single, 13, 1169));

        Assert.Equal(("%X (Percent X)", 18), (found.Chart.SongName, found.Chart.Level));
    }

    [Fact]
    public void JudgmentsNoChartOfTheSongAddsUpToAreAContradiction()
    {
        var contradicted = Assert.IsType<CatalogMatch.Contradicted>(Arcade.Match("VANISH", ChartType.Single, 20, 1300));

        Assert.Equal((1222, 1300), (contradicted.Chart.NoteCount, contradicted.Notes));
    }

    [Fact]
    public void AChartWhoseNotesAreUnknownIsTakenAsRead()
    {
        Assert.Equal("D", Named(Arcade.Match("D", ChartType.Single, 12, 400)));
        Assert.Equal("VANISH", Named(Arcade.Match("VANISH", ChartType.Single, 20)));
    }
}

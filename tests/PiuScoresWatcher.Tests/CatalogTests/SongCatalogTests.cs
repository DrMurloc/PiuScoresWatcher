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
        // Aragami has no S20: the list lacks the chart the screen shows (D70)
        Assert.Equal("Aragami", Assert.IsType<CatalogMatch.Unlisted>(Catalog.Match("Aragami", ChartType.Single, 20)).SongName);
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

    /// <summary>A few of Rise's songs as the site lists them, the look-alikes among them, at levels the tester's run met.</summary>
    private static readonly SongCatalog Rise = new(
    [
        Chart("Love is a Danger Zone", ChartType.Single, 17),
        Chart("Love is a Danger Zone(Cranky Mix)", ChartType.Single, 17),
        Chart("Love is a Danger Zone(Cranky Mix)", ChartType.Single, 14),
        Chart("Conflict", ChartType.Single, 11),
        Chart("Conflict -NOMA CONCEiVER REMiX-", ChartType.Single, 11),
        Chart("Conflict -NOMA CONCEiVER REMiX-", ChartType.Single, 16),
        Chart("The Quick Brown Fox Jumps Over The Lazy Dog", ChartType.Single, 19),
        Chart("Waltz of Doge", ChartType.Single, 19),
        Chart("wanna go to the moon palace", ChartType.Single, 21),
        Chart("Burning SuperNova feat. neur6sia", ChartType.Single, 21),
        Chart("Burn Out", ChartType.Single, 21),
        Chart("Pumping Up", ChartType.Single, 8),
        Chart("Pumping Up", ChartType.Single, 5),
        Chart("The People didn't know \"Pumping up\"", ChartType.Single, 8),
        Chart("Elise", ChartType.Single, 18),
        Chart("Sorceress Elise", ChartType.Single, 18),
        Chart("D", ChartType.Single, 4),
        Chart("See", ChartType.Single, 19),
        Chart("Elysium", ChartType.Single, 3),
        Chart("Elysium", ChartType.Single, 9),
        Chart("The De[i]fied", ChartType.Single, 14)
    ]);

    private static readonly TitleShape CutOffInTheList = new(true, 30);
    private static readonly TitleShape WholeInTheList = new(false, 30);
    private static readonly TitleShape CutOffOnThePanel = new(true, 20);
    private static readonly TitleShape WholeOnThePanel = new(false, 20);

    [Theory]
    [InlineData("Love is a Danger Zone(Cr", 17, "Love is a Danger Zone(Cranky Mix)")] // never the song it starts with
    [InlineData("Love is a Danger Zone", 17, "Love is a Danger Zone(Cranky Mix)")]
    [InlineData("Conflict -", 11, "Conflict -NOMA CONCEiVER REMiX-")]
    [InlineData("Conflict -NOMA CONCEIVEF", 16, "Conflict -NOMA CONCEiVER REMiX-")] // the edge cut the last letter in half
    [InlineData("The Quick Brown Fo", 19, "The Quick Brown Fox Jumps Over The Lazy Dog")]
    [InlineData("The Quick Brown Fox JumF", 19, "The Quick Brown Fox Jumps Over The Lazy Dog")]
    [InlineData("vanna go to the moon palat", 21, "wanna go to the moon palace")] // and one misread letter
    public void ATitleCutOffAtItsBoxsEdgeIsTheStartOfALongerOne(string read, int level, string expected)
    {
        Assert.Equal(expected, Named(Rise.Match(read, ChartType.Single, level, shape: CutOffOnThePanel)));
    }

    [Fact]
    public void TheSameReadingWholeIsTheShorterSong()
    {
        Assert.Equal("Love is a Danger Zone", Named(Rise.Match("Love is a Danger Zone", ChartType.Single, 17, shape: WholeInTheList)));
        Assert.Equal("Conflict", Named(Rise.Match("Conflict", ChartType.Single, 11, shape: WholeInTheList)));
    }

    [Theory]
    [InlineData("Bur", 21)] // the start of Burning SuperNova, or of Burn Out
    [InlineData("wanr", 21)]
    [InlineData("ck Brown Fox Jumps Over T", 19)] // a middle, cut at both ends: not the start of anything
    [InlineData("Elysium", 4)] // a start names no chart the list lacks
    public void WhatRanOffTheEdgeNamesNothingWhenItIsNotTheStartOfOneTitle(string read, int level)
    {
        Assert.IsType<CatalogMatch.NotFound>(Rise.Match(read, ChartType.Single, level, shape: CutOffInTheList));
    }

    [Fact]
    public void AReadingThatCouldBeTheTailOfATitleScrollingOutNamesNothing()
    {
        // The People didn't know "Pumping up" leaving the panel shows "Pumping up", another song's whole title
        Assert.IsType<CatalogMatch.NotFound>(Rise.Match("Pumping up", ChartType.Single, 8, shape: WholeOnThePanel));
        // "Dog" is the end of The Quick Brown Fox as much as it is in Waltz of Doge
        Assert.IsType<CatalogMatch.NotFound>(Rise.Match("Dog", ChartType.Single, 19, shape: WholeOnThePanel));
    }

    [Fact]
    public void ATitleTooShortToScrollIsNoOnesTail()
    {
        // Sorceress Elise fits the list's row and the panel; on a result screen no title scrolls at all
        Assert.Equal("Elise", Named(Rise.Match("Elise", ChartType.Single, 18, shape: WholeOnThePanel)));
        Assert.Equal("Pumping Up", Named(Rise.Match("Pumping up", ChartType.Single, 8)));
    }

    [Theory]
    [InlineData("N", 4)] // one letter from D
    [InlineData("Bee", 19)] // one letter from See
    public void ATitleUnderFourLettersMatchesExactlyOrNotAtAll(string read, int level)
    {
        Assert.IsType<CatalogMatch.NotFound>(Rise.Match(read, ChartType.Single, level));
    }

    [Fact]
    public void AShortTitleReadExactlyIsFound()
    {
        Assert.Equal("D", Named(Rise.Match("D", ChartType.Single, 4)));
        Assert.Equal("See", Named(Rise.Match("See", ChartType.Single, 19)));
    }

    [Theory]
    [InlineData("Elysium", 4, "Elysium")] // the game's Elysium S4 is PIU Scores' S3
    [InlineData("The De[ilfied", 15, "The De[i]fied")] // a misread letter, and a level the list has wrong
    public void AWholeTitleTheListHasOnlyAtOtherChartsIsUnlisted(string read, int level, string song)
    {
        var unlisted = Assert.IsType<CatalogMatch.Unlisted>(Rise.Match(read, ChartType.Single, level, shape: WholeInTheList));

        Assert.Equal(song, unlisted.SongName);
    }

    [Fact]
    public void ATitleNoSongHasIsNotUnlisted()
    {
        Assert.IsType<CatalogMatch.NotFound>(Rise.Match("Festival of Death Moon", ChartType.Single, 21, shape: WholeInTheList));
    }

    [Fact]
    public void ATailIsNotTakenForAnUnlistedSong()
    {
        // at S8 "Pumping up" may be The People didn't know "Pumping up" leaving the panel, and that chart is listed
        var catalog = new SongCatalog([Chart("Pumping Up", ChartType.Single, 5), Chart("The People didn't know \"Pumping up\"", ChartType.Single, 8)]);

        Assert.IsType<CatalogMatch.NotFound>(catalog.Match("Pumping up", ChartType.Single, 8, shape: WholeOnThePanel));
        // where no title scrolls, a result screen's, it is Pumping Up at a level the list lacks
        Assert.IsType<CatalogMatch.Unlisted>(catalog.Match("Pumping up", ChartType.Single, 8));
    }
}

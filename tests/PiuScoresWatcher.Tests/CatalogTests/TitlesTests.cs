using Moq;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Catalog;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.CatalogTests;

/// <summary>
///     A song list's title chosen box by box (D66, D68, D70): the tester's screens through the real reader, the OCR
///     stubbed with what Windows read off each box of them.
/// </summary>
public sealed class TitlesTests
{
    private const string MoonPalace = "20260924233850";     // S21: whole in the list's row, its start cut off on the panel
    private const string QuickBrownFox = "20260925003727";  // S19: its tail leaving the row, its start coming back onto the panel
    private const string PumpingUp = "20260925003656";      // The People didn't know "Pumping up" S8: its tail on the panel
    private const string ElysiumS4 = "20260925000000";      // a level PIU Scores' Rise list had wrong

    private static readonly SongCatalog Rise = new(
    [
        Chart("wanna go to the moon palace", 21),
        Chart("The Quick Brown Fox Jumps Over The Lazy Dog", 19),
        Chart("Waltz of Doge", 19),
        Chart("The People didn't know \"Pumping up\"", 8),
        Chart("Pumping Up", 8), // made up: Pumping Up has no S8, and a tail must not take it for that song all the same
        Chart("Elysium", 3),
        Chart("Elysium", 9),
        Chart("Elysium", 14)
    ]);

    private readonly Mock<ITitleReader> _titles = new();

    private static CatalogChart Chart(string song, int level)
    {
        return new CatalogChart(Guid.NewGuid(), song, ChartType.Single, level);
    }

    private void Reads(string box, params string[] reads)
    {
        _titles.Setup(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(b => b.Name == box), It.IsAny<CancellationToken>()))
            .Returns(() => TitleReads.Of(reads));
    }

    private Task<TitleChoice> ChooseAsync(string fixture, int level, SongCatalog? catalog)
    {
        var image = FixtureScreens.Load(fixture);
        return _titles.Object.ChooseAsync(image, new SongListReader().Read(image)!.Titles, catalog, ChartType.Single, level, null, CancellationToken.None);
    }

    [Fact]
    public async Task TheListsRowIsReadFirstAndThePanelOnlyWhenTheRowNamesNothing()
    {
        Reads("list", "wanna go to the moon palace");
        Reads("panel", "wanna go to the moon pali");

        var choice = await ChooseAsync(MoonPalace, 21, Rise);

        Assert.Equal("wanna go to the moon palace", Assert.IsType<CatalogMatch.Found>(choice.Match).Chart.SongName);
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(b => b.Name == "panel"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ATitleScrollingInBothPlacesIsFoundByItsStartComingBackOntoThePanel()
    {
        // the row's tail names nothing (and "Dog" alone would be Waltz of Doge's as much); the panel's start runs off
        // its edge, so it is the start of a longer title
        Reads("list", "Over The Lazy Dog", "Over The Lazy Doo");
        Reads("panel", "The Quick Brown Fo");

        var choice = await ChooseAsync(QuickBrownFox, 19, Rise);

        Assert.Equal("The Quick Brown Fox Jumps Over The Lazy Dog", Assert.IsType<CatalogMatch.Found>(choice.Match).Chart.SongName);
        Assert.Equal("The Quick Brown Fo", choice.Read);
    }

    [Fact]
    public async Task TheTailOfATitleLeavingThePanelIsNotTakenForAnotherSong()
    {
        Reads("list");
        Reads("panel", "Pumping up");

        var choice = await ChooseAsync(PumpingUp, 8, Rise);

        Assert.IsType<CatalogMatch.NotFound>(choice.Match);
    }

    [Fact]
    public async Task TheSameScreensRowNamesItsSong()
    {
        Reads("list", "The People didn't know Pumping up");
        Reads("panel", "Pumping up");

        var choice = await ChooseAsync(PumpingUp, 8, Rise);

        Assert.Equal("The People didn't know \"Pumping up\"", Assert.IsType<CatalogMatch.Found>(choice.Match).Chart.SongName);
    }

    [Fact]
    public async Task ASongTheListHasOnlyAtOtherChartsIsWhatTheChoiceSaysWhenNothingNamesTheChart()
    {
        Reads("list", "Elysium");
        Reads("panel", "Elysium");

        var choice = await ChooseAsync(ElysiumS4, 4, Rise);

        Assert.Equal("Elysium", Assert.IsType<CatalogMatch.Unlisted>(choice.Match).SongName);
        _titles.Verify(t => t.ReadAsync(It.IsAny<ScreenImage>(), It.Is<TitleBox>(b => b.Name == "panel"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EachBoxsFirstReadingIsWhatWasSeen()
    {
        Reads("list", "Over The Lazy Dog", "Over The Lazy Doo");
        Reads("panel", "The Quick Brown Fo");

        var choice = await ChooseAsync(QuickBrownFox, 19, new SongCatalog([]));

        Assert.Equal([new BoxReading("list", "Over The Lazy Dog"), new BoxReading("panel", "The Quick Brown Fo")], choice.Seen);
        Assert.Equal("'Over The Lazy Dog' (list) / 'The Quick Brown Fo' (panel)", choice.Described);
    }

    [Fact]
    public async Task WithoutAChartListTheFirstReadingStands()
    {
        Reads("list", "wanna go to the moon palace");
        Reads("panel", "wanna go to the moon pali");

        var choice = await ChooseAsync(MoonPalace, 21, null);

        Assert.Equal("wanna go to the moon palace", choice.Read);
        Assert.IsType<CatalogMatch.NotFound>(choice.Match);
    }

    [Fact]
    public async Task NothingReadAnywhereIsSeenAsNothing()
    {
        Reads("list");
        Reads("panel");

        var choice = await ChooseAsync(MoonPalace, 21, Rise);

        Assert.Null(choice.Read);
        Assert.Equal("nothing", choice.Described);
    }
}

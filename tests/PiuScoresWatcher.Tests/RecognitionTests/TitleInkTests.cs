using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.RecognitionTests;

public sealed class TitleInkTests
{
    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(236, 238, 240)]
    public void WhiteIsAllInk(byte red, byte green, byte blue)
    {
        Assert.Equal(1.0, TitleInk.Ink(red, green, blue), 3);
    }

    [Theory]
    [InlineData(250, 200, 40)] // the yellow bar behind a Warm Up title
    [InlineData(120, 230, 255)] // a bright cyan streak in a jacket
    [InlineData(40, 90, 230)] // the Arcade Station's blue band
    [InlineData(128, 128, 128)] // a grey
    [InlineData(20, 20, 24)] // the dark outline around the letters
    public void ColoursAndShadowsAreNoInk(byte red, byte green, byte blue)
    {
        Assert.Equal(0.0, TitleInk.Ink(red, green, blue), 3);
    }

    [Fact]
    public void ALetterEdgeIsPartInk()
    {
        var edge = TitleInk.Ink(190, 190, 192);

        Assert.InRange(edge, 0.3, 0.8);
    }

    [Fact]
    public void WhiteLettersOnTheBarAreDarkAndEverythingElseIsPaper()
    {
        // a white letter stroke on the yellow bar, and a bright cyan patch beside it
        var image = Paint(1920, 1080, (250, 200, 40), [(new PixelRect(110, 20, 130, 60), (255, 255, 255)), (new PixelRect(150, 20, 190, 60), (120, 230, 255))]);

        var page = TitleInk.Render(image, new PixelRect(100, 10, 200, 70));

        Assert.Equal(0, Shade(page, 20, 30)); // the letter
        Assert.Equal(255, Shade(page, 5, 30)); // the bar
        Assert.Equal(255, Shade(page, 70, 30)); // the cyan patch
    }

    [Theory]
    [InlineData(1920, 1080, 100, 20)]
    [InlineData(1280, 720, 150, 30)]
    [InlineData(3840, 2160, 50, 10)]
    public void ThePageIsTheSizeTheTitleHasAt1080p(int width, int height, int pageWidth, int pageHeight)
    {
        var image = Paint(width, height, (0, 0, 0), []);

        var page = TitleInk.Render(image, new PixelRect(0, 0, 100, 20));

        Assert.Equal((pageWidth, pageHeight), (page.Width, page.Height));
        Assert.Equal(pageWidth * pageHeight, page.Pixels.Length);
    }

    [Fact]
    public void ThePagesAreTheTitleAtItsSizeThenDoubleThenWithItsGapsClosedThenThinnedEachWithPaperAround()
    {
        // D55: without the margin Windows OCR read nothing of VANISH, Cynical or Aragami
        var image = Paint(1920, 1080, (250, 200, 40), [(new PixelRect(110, 20, 130, 60), (255, 255, 255)), (new PixelRect(170, 20, 190, 60), (255, 255, 255))]);
        var region = new PixelRect(100, 10, 200, 70);

        var pages = TitleInk.Pages(image, TitleBox.ResultTitle(region)).Select(page => page.Image).Take(4).ToList();

        Assert.Equal((100 + 2 * TitleInk.Margin, 60 + 2 * TitleInk.Margin), (pages[0].Width, pages[0].Height));
        Assert.Equal((200 + 4 * TitleInk.Margin, 120 + 4 * TitleInk.Margin), (pages[1].Width, pages[1].Height));
        Assert.True(pages[2].Width < pages[0].Width); // the 40-pixel gap between the two strokes is closed up
        Assert.Equal((pages[2].Width, pages[2].Height), (pages[3].Width, pages[3].Height));
        Assert.True(pages[3].Pixels.Count(shade => shade < 128) < pages[2].Pixels.Count(shade => shade < 128)); // and thinned
        Assert.Equal(255, Shade(pages[0], 5, 5));
        Assert.Equal(0, Shade(pages[0], TitleInk.Margin + 15, TitleInk.Margin + 30));
    }

    [Fact]
    public void OnlyATitleInHeavyTypeIsThinned()
    {
        // the list's row prints in a light type that thinning wipes out ("Morrighan" read as "_linan")
        var image = Paint(1920, 1080, (250, 200, 40), [(new PixelRect(110, 20, 330, 60), (255, 255, 255))]);
        var region = new PixelRect(100, 10, 400, 70);

        Assert.Equal(4, TitleInk.Pages(image, new TitleBox("panel", region, true, 20)).Count());
        Assert.Equal(3, TitleInk.Pages(image, new TitleBox("list", region, false, 30)).Count());
    }

    [Fact]
    public void AShortTitleIsAlsoTriedThreeTimesOverAtTwoSpacings()
    {
        // D67: Windows reads nothing of B2 on any page of it alone, and "B2 B2 B2" off a page of copies
        var image = FixtureScreens.Load("20260924233951");
        var row = new SongListReader().Read(image)!.Titles[0];

        var pages = TitleInk.Pages(image, row).ToList();

        var copies = pages.Where(page => page.Repeats == 3).ToList();
        Assert.Equal(2, copies.Count);
        Assert.Equal(pages.Count - 2, pages.FindIndex(page => page.Repeats == 3));
        var word = TitleInk.Trim(TitleInk.Render(image, row.Region))!;
        Assert.All(copies, page => Assert.True(page.Image.Width > 3 * word.Width + 2 * TitleInk.Margin));
        Assert.True(copies[1].Image.Width > copies[0].Image.Width); // the second spacing is the wider
    }

    [Theory]
    [InlineData("20260924233850")] // wanna go to the moon palace
    [InlineData("20260923193156")] // Morrighan
    public void ALongerTitleIsNotRepeated(string fixture)
    {
        var image = FixtureScreens.Load(fixture);

        Assert.All(new SongListReader().Read(image)!.Titles,
            box => Assert.DoesNotContain(TitleInk.Pages(image, box), page => page.Repeats > 1));
    }

    [Theory]
    [InlineData("B2 B2 B2", "B2")]
    [InlineData("Dr.M Dr.M Dr.M", "Dr.M")]
    [InlineData("8 6 8 6 8 6", "8 6")]
    [InlineData("NN N", null)]
    [InlineData("82 B2 B2", null)]
    [InlineData("D D", null)]
    public void APageOfCopiesReadsAsTheOneWordTheyAllSay(string text, string? title)
    {
        var page = new TitlePage(new GrayImage(1, 1, [255]), 3);

        Assert.Equal(title, page.Reading(text));
    }

    [Fact]
    public void APageOfOneTitleReadsAsItIs()
    {
        Assert.Equal("NN N", new TitlePage(new GrayImage(1, 1, [255]), 1).Reading("NN N"));
    }

    [Theory]
    [InlineData("20260925003645", 0, true)]  // The Quick Brown Fox at its start: too long for the list's row too
    [InlineData("20260925003645", 1, true)]
    [InlineData("20260924233850", 0, false)] // wanna go to the moon palace: whole in the row,
    [InlineData("20260924233850", 1, true)]  // its start on the panel
    [InlineData("20260925003727", 0, false)] // The Quick Brown Fox's tail scrolling out of the row,
    [InlineData("20260925003727", 1, true)]  // its start coming back onto the panel
    [InlineData("20260924235245", 1, true)]  // "Conflict -", the remix's start coming back in
    [InlineData("20260925003656", 1, false)] // "Pumping up", a tail: it ends where the title ends
    [InlineData("20260925000000", 0, false)] // Elysium, whole in both
    [InlineData("20260925000000", 1, false)]
    [InlineData("20260923193126", 0, false)] // the owner's 1080p L (PIU Edit)
    [InlineData("20260923193126", 1, false)]
    public void ATitleCutOffAtItsBoxsEdgeRunsOffIt(string fixture, int box, bool runsOff)
    {
        var image = FixtureScreens.Load(fixture);

        Assert.Equal(runsOff, TitleInk.RunsOffRight(image, new SongListReader().Read(image)!.Titles[box].Region));
    }

    [Fact]
    public void ClosingGapsLeavesTheLettersAndANarrowSpaceBetweenThem()
    {
        var page = TitleInk.Render(Paint(1920, 1080, (0, 0, 0), [(new PixelRect(10, 0, 20, 40), (255, 255, 255)), (new PixelRect(80, 0, 90, 40), (255, 255, 255))]),
            new PixelRect(0, 0, 100, 40));

        var closed = TitleInk.CloseGaps(page);

        // ahead of the first stroke 10 columns stay (under 0.4 of 40), the 60-column gap becomes 6, the 10 after stay
        Assert.Equal(10 + 10 + 6 + 10 + 10, closed.Width);
        Assert.Equal(page.Height, closed.Height);
    }

    [Fact]
    public void MorrighansTitleOnTheSongListIsLettersOnCleanPaper()
    {
        // The title the old cut read as nothing: the jacket behind it is full of bright colour.
        var image = FixtureScreens.Load("20260923193156");
        var reading = new SongListReader().Read(image)!;

        var page = TitleInk.Render(image, reading.Titles[1].Region);

        var ink = page.Pixels.Count(shade => shade < 128) / (double)page.Pixels.Length;
        Assert.InRange(ink, 0.05, 0.4);
        // right of the word, to the edge of the box — short of the card's white edge now (D66) — there is only art,
        // and no speck of it becomes ink
        var from = page.Width * 3 / 4;
        var rightOfTheWord = Enumerable.Range(0, page.Height).SelectMany(y => Enumerable.Range(from, page.Width - from).Select(x => Shade(page, x, y)));
        Assert.All(rightOfTheWord, shade => Assert.True(shade >= 128));
    }

    private static byte Shade(GrayImage page, int x, int y)
    {
        return page.Pixels[y * page.Width + x];
    }

    private static ScreenImage Paint(int width, int height, (byte R, byte G, byte B) fill, (PixelRect Area, (byte R, byte G, byte B) Colour)[] areas)
    {
        var bgra = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var colour = fill;
            foreach (var (area, paint) in areas)
                if (x >= area.X0 && x < area.X1 && y >= area.Y0 && y < area.Y1)
                    colour = paint;
            var offset = (y * width + x) * 4;
            bgra[offset] = colour.B;
            bgra[offset + 1] = colour.G;
            bgra[offset + 2] = colour.R;
            bgra[offset + 3] = 255;
        }

        return new ScreenImage(width, height, bgra);
    }
}

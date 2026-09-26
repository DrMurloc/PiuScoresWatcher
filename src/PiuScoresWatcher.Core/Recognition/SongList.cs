using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>Where Warm Up's song list puts the lit chart and its best, measured on the owner's 1080p screens (tools/reader-lab/songlist.py).</summary>
internal static class SongListLayout
{
    public const int Boxes = 6;

    /// <summary>The yellow WARM UP banner; no other screen puts that much yellow there.</summary>
    public static readonly FractionRect Banner = FractionRect.At1080p(140, 30, 630, 110);

    public static readonly FractionRect SingleTab = FractionRect.At1080p(174, 492, 383, 540);
    public static readonly FractionRect HalfDoubleTab = FractionRect.At1080p(398, 492, 607, 540);
    public static readonly FractionRect Score = FractionRect.At1080p(320, 636, 452, 668);
    public static readonly FractionRect Badge = FractionRect.At1080p(470, 640, 600, 750);

    /// <summary>
    ///     The lit row's title in the list, as far as the game shows it: about 37 characters of a smaller type, so all but
    ///     two of Rise's titles hold still here, where the panel scrolls 28 of them (D66).
    /// </summary>
    public static readonly FractionRect RowTitle = FractionRect.At1080p(872, 576, 1433, 618);

    /// <summary>
    ///     The panel's title, over the song's video, as far as the game shows it — short of the card's white edge, which
    ///     Windows OCR took for a letter (D66). A title longer than about 24 characters scrolls through it.
    /// </summary>
    public static readonly FractionRect PanelTitle = FractionRect.At1080p(80, 388, 606, 440);

    /// <summary>A title longer than this may scroll in the row, or on the panel: kept short of each box's measure (D68).</summary>
    public const int RowScrollsPast = 30;

    public const int PanelScrollsPast = 20;

    /// <summary>
    ///     The lit song's jacket in the list on the right, which keeps the lit song in its fourth row on every one of the
    ///     owner's screens and the first tester's. The panel's title sits over the song's video; the jacket holds still.
    /// </summary>
    public static readonly FractionRect LitJacket = FractionRect.At1080p(792, 572, 858, 648);

    /// <summary>The two places the lit song's title is read, in the order they are tried (D66).</summary>
    public static IReadOnlyList<TitleBox> Titles(ScreenImage image)
    {
        return
        [
            new TitleBox("list", RowTitle.On(image), false, RowScrollsPast),
            new TitleBox("panel", PanelTitle.On(image), true, PanelScrollsPast)
        ];
    }

    /// <summary>A level box's yellow area: the lit one is the selected chart.</summary>
    public static FractionRect Box(int i)
    {
        var x0 = 83 + 90 * i;
        return FractionRect.At1080p(x0 + 4, 562, x0 + 72, 630);
    }

    /// <summary>A level box's digits: right of the H stamp that overlaps its corner, above the three bars.</summary>
    public static FractionRect BoxDigits(int i)
    {
        var x0 = 83 + 90 * i;
        return FractionRect.At1080p(x0 + 21, 574, x0 + 66, 604);
    }

    /// <summary>The list's rows, counted from the top; the lit song's is the fourth (D66).</summary>
    public const int Rows = 7;

    public const int LitRow = 4;

    /// <summary>A row's places for a level, right-aligned: a song with fewer charts leaves the left ones empty (D75).</summary>
    public const int Places = 6;

    // Measured on the first tester's 4K lists, halved (tools/reader-lab/songlist.py): the first row's bars' centre line and
    // the row pitch, the first place's left edge and the place pitch, and each bar's inside, clear of its dark edges.
    private const double FirstBarsY = 300.8;
    private const double RowPitch = 108.36;
    private const double FirstPlaceX = 1454;
    private const double PlacePitch = 62;
    private const double BarHalfHeight = 1.5;
    private static readonly (double X0, double X1)[] BarSpans = [(1, 11), (15, 25), (29, 40)];

    /// <summary>One of the three bars under a place's level, the first leftmost: lit gold for the chart's best mark (D74).</summary>
    public static FractionRect Bar(int row, int place, int bar)
    {
        var x0 = FirstPlaceX + PlacePitch * place;
        var y = BarsY(row);
        return At1080p(x0 + BarSpans[bar].X0, y - BarHalfHeight, x0 + BarSpans[bar].X1, y + BarHalfHeight);
    }

    /// <summary>A place's level, above its bars; the H stamp sits over its top left, as it does on the panel's boxes.</summary>
    public static FractionRect PlaceDigits(int row, int place)
    {
        var x0 = FirstPlaceX + PlacePitch * place;
        var y = BarsY(row);
        return At1080p(x0 - 6, y - 40, x0 + 48, y - 6);
    }

    /// <summary>Right of a row's last place, where nothing but the row is drawn: yellow on the lit row, navy on the others.</summary>
    public static FractionRect RowBackground(int row)
    {
        var y = BarsY(row);
        return At1080p(1812, y - 14, 1880, y + 14);
    }

    /// <summary>A row's title box: the lit row's, moved to the row.</summary>
    public static FractionRect RowTitleOf(int row)
    {
        return Moved(RowTitle, row);
    }

    /// <summary>A row's jacket: the lit one's place, moved to the row. A song shows the same jacket in every row it scrolls through.</summary>
    public static FractionRect JacketOf(int row)
    {
        return Moved(LitJacket, row);
    }

    private static double BarsY(int row)
    {
        return FirstBarsY + RowPitch * (row - 1);
    }

    private static FractionRect Moved(FractionRect litRow, int row)
    {
        var dy = (BarsY(row) - BarsY(LitRow)) / 1080;
        return litRow with { Y0 = litRow.Y0 + dy, Y1 = litRow.Y1 + dy };
    }

    private static FractionRect At1080p(double x0, double y0, double x1, double y1)
    {
        return new FractionRect(x0 / 1920, y0 / 1080, x1 / 1920, y1 / 1080);
    }
}

public enum SongListStatus
{
    /// <summary>The lit chart's best, read and agreeing with its grade.</summary>
    Best,

    /// <summary>The lit chart has no best yet: nothing to capture.</summary>
    NoBest,

    /// <summary>A number no template claims, or a badge no grade matches; kept for review, never sent.</summary>
    Unreadable,

    /// <summary>The badge is a different grade from the one the score earns — a misread number (D47).</summary>
    GradeDisagrees,

    /// <summary>The list, with a chart lit that can't be placed: nothing is read off it (D72).</summary>
    Unplaced
}

/// <summary>
///     What the song-list reader made of one frame. The title is read by an adapter from <see cref="Titles" />, the lit
///     row's and then the panel's (D66); <see cref="Jacket" /> is a print of the lit song's jacket, which tells two songs
///     apart when their panels read the same. <see cref="Rows" /> are the list's other rows, whatever the lit chart's
///     status (D74). A list whose lit chart can't be placed has no chart type, and nothing else is read (D72).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SongListReading(
    SongListStatus Status,
    string? Reason,
    ChartType? ChartType,
    int? Level,
    int? Score,
    string? Grade,
    IReadOnlyList<TitleBox> Titles,
    ulong Jacket,
    IReadOnlyList<SongListRow> Rows);

/// <summary>
///     One of the song list's rows other than the lit one, as far as its marks go (D74, D75): under each level three bars
///     light gold from the left for the chart's best mark — one No Miss, two Full Combo, three a Perfect Game, which is
///     1,000,000 and so a whole capture.
/// </summary>
/// <param name="Row">Where it sits, counted from the top; never <see cref="SongListLayout.LitRow" />.</param>
/// <param name="Jacket">A print of its jacket: the same song prints the same in whichever row it scrolls through.</param>
/// <param name="Marks">
///     Each chart's lit bars, left to right; null when the row can't be read on this frame — a bar neither lit nor unlit, or
///     a Perfect Game whose level doesn't read — and empty for a row with no chart.
/// </param>
/// <param name="PerfectGames">The levels of the charts with all three bars lit, left to right; empty when <paramref name="Marks" /> is null.</param>
/// <param name="Title">The row's title box, read only when it has a Perfect Game to send.</param>
[ExcludeFromCodeCoverage]
public sealed record SongListRow(int Row, ulong Jacket, IReadOnlyList<int>? Marks, IReadOnlyList<int> PerfectGames, TitleBox Title);

/// <summary>What the detector saw of Warm Up's song list on a frame that shows it.</summary>
[ExcludeFromCodeCoverage]
public abstract record SongListSighting
{
    /// <summary>The lit chart: its tab's chart type, and its level box counted from the left.</summary>
    public sealed record Lit(ChartType ChartType, int Box) : SongListSighting;

    /// <summary>A tab or a level box lit, and not exactly one of each: which chart is lit can't be told (D72).</summary>
    public sealed record Unplaced(string Reason) : SongListSighting;
}

/// <summary>
///     Is this frame Warm Up's song list, and which chart is lit? The yellow banner, exactly one lit tab — 5K
///     SINGLE's orange or 6K DOUBLE's blue (D71) — and exactly one lit level box (yellow). The Arcade Station's list
///     has none of them (D45). The banner with a tab or a box lit, but not one of each, is the list with a chart that
///     can't be placed (D72): 6K DOUBLE's blue tab was that, before D71.
/// </summary>
public sealed class SongListDetector
{
    public const double MinimumBannerShare = 0.2;
    public const double MinimumTabShare = 0.08;
    public const double MinimumBoxShare = 0.3;

    /// <summary>The list as seen, or null when the frame is not Warm Up's song list.</summary>
    public SongListSighting? Detect(ScreenImage image)
    {
        if (Colors.Fraction(image, SongListLayout.Banner.On(image), ColorClass.BannerYellow) < MinimumBannerShare)
            return null;
        List<ChartType> tabs = [];
        if (Colors.Fraction(image, SongListLayout.SingleTab.On(image), ColorClass.TabOrange) >= MinimumTabShare)
            tabs.Add(ChartType.Single);
        if (Colors.Fraction(image, SongListLayout.HalfDoubleTab.On(image), ColorClass.TabBlue) >= MinimumTabShare)
            tabs.Add(ChartType.HalfDouble);
        var boxes = Enumerable.Range(0, SongListLayout.Boxes)
            .Where(i => Colors.Fraction(image, SongListLayout.Box(i).On(image), ColorClass.BoxYellow) >= MinimumBoxShare)
            .ToList();
        if (tabs.Count == 1 && boxes.Count == 1)
            return new SongListSighting.Lit(tabs[0], boxes[0]);
        return tabs.Count == 0 && boxes.Count == 0 ? null : new SongListSighting.Unplaced(Described(tabs, boxes));
    }

    /// <summary>What the list showed lit, for the note beside the frame and the log.</summary>
    private static string Described(List<ChartType> tabs, List<int> boxes)
    {
        var tab = tabs.Count switch
        {
            0 => "no tab lit (5K SINGLE orange, 6K DOUBLE blue)",
            1 => $"the {tabs[0]} tab lit",
            _ => "both tabs lit"
        };
        var box = boxes.Count switch
        {
            0 => "no level box lit",
            1 => $"level box {boxes[0] + 1} lit",
            _ => $"level boxes {string.Join(", ", boxes.Select(i => i + 1))} lit"
        };
        return $"the song list with {tab} and {box}";
    }
}

/// <summary>
///     Reads the lit chart's best off Warm Up's song list: the level from the lit box, the best score, and
///     the grade badge as the check (D47). The panel's accuracy and max combo are left alone: they do not
///     always come from the best-score play. The other rows are read for their marks, and the level of each chart
///     whose three bars are lit: a Perfect Game (D74, D75).
/// </summary>
public sealed class SongListReader
{
    /// <summary>How much of a bar must be the gold of a lit one, or the grey of an unlit one, for it to count as either.</summary>
    public const double MinimumBarShare = 0.6;

    /// <summary>The lit row shows at least 0.29 of its yellow right of its places on every fixture; the others show none.</summary>
    public const double MinimumLitRowShare = 0.1;

    private readonly SongListDetector _detector = new();
    private readonly GradeBadges _grades;
    private readonly TemplateSet _templates;

    public SongListReader()
    {
        _templates = TemplateSet.Embedded;
        _grades = GradeBadges.Embedded;
    }

    /// <summary>The reading, or null when the frame is not Warm Up's song list.</summary>
    public SongListReading? Read(ScreenImage image)
    {
        var sighting = _detector.Detect(image);
        if (sighting is SongListSighting.Unplaced unplaced)
            return new SongListReading(SongListStatus.Unplaced, unplaced.Reason, null, null, null, null, [], 0, []);
        if (sighting is not SongListSighting.Lit lit)
            return null;
        var titles = SongListLayout.Titles(image);
        var jacket = Colors.LuminancePrint(image, SongListLayout.LitJacket.On(image));
        var rows = Rows(image);
        var level = NumberFieldReader.Read(image, SongListLayout.BoxDigits(lit.Box).On(image), MaskKind.Light, TemplateFamilies.ListLevel, _templates);
        var score = NumberFieldReader.Read(image, SongListLayout.Score.On(image), MaskKind.Light, TemplateFamilies.ListValue, _templates);

        if (score.IsEmpty)
            return new SongListReading(SongListStatus.NoBest, null, lit.ChartType, level.Value, null, null, titles, jacket, rows);
        if (!level.IsClean || level.Value is not (>= 1 and <= 29))
            return new SongListReading(SongListStatus.Unreadable, $"level read as '{level.Text}'", lit.ChartType, null, score.Value, null, titles,
                jacket, rows);
        if (!score.IsClean || score.Value is not (>= 0 and <= 1_000_000))
            return new SongListReading(SongListStatus.Unreadable, $"best score read as '{score.Text}'", lit.ChartType, level.Value, null, null,
                titles, jacket, rows);

        var grade = _grades.Classify(image, SongListLayout.Badge.On(image));
        if (!grade.IsConfident)
            return new SongListReading(SongListStatus.Unreadable,
                $"the grade badge matched no grade clearly ({grade.Grade} at {grade.Similarity:0.00}, ahead by {grade.Margin:0.00})",
                lit.ChartType, level.Value, score.Value, null, titles, jacket, rows);

        var earned = Grades.Of(RiseMix.Rise, score.Value.Value);
        return earned == grade.Grade
            ? new SongListReading(SongListStatus.Best, null, lit.ChartType, level.Value, score.Value, grade.Grade, titles, jacket, rows)
            : new SongListReading(SongListStatus.GradeDisagrees, $"the badge shows {grade.Grade}, {score.Value} earns {earned}",
                lit.ChartType, level.Value, score.Value, grade.Grade, titles, jacket, rows);
    }

    /// <summary>Every row but the lit one, which its yellow gives away (D75).</summary>
    private IReadOnlyList<SongListRow> Rows(ScreenImage image)
    {
        List<SongListRow> rows = [];
        for (var row = 1; row <= SongListLayout.Rows; row++)
        {
            if (Colors.Fraction(image, SongListLayout.RowBackground(row).On(image), ColorClass.LitRow) >= MinimumLitRowShare)
                continue;
            var jacket = Colors.LuminancePrint(image, SongListLayout.JacketOf(row).On(image));
            var title = new TitleBox($"row {row}", SongListLayout.RowTitleOf(row).On(image), false, SongListLayout.RowScrollsPast);
            rows.Add(Marks(image, row) is { } read
                ? new SongListRow(row, jacket, read.Marks, read.PerfectGames, title)
                : new SongListRow(row, jacket, null, [], title));
        }

        return rows;
    }

    /// <summary>
    ///     A row's marks, place by place from the left, and the level of each Perfect Game; null when the row can't be read
    ///     on this frame. A place holds a chart when each of its bars is lit or unlit, the lit ones from the left, and is
    ///     empty when none is either; the places are right-aligned, so an empty one after a chart means the row is not
    ///     sitting where it should, moving or covered.
    /// </summary>
    private (IReadOnlyList<int> Marks, IReadOnlyList<int> PerfectGames)? Marks(ScreenImage image, int row)
    {
        List<int> marks = [];
        List<int> perfectGames = [];
        for (var place = 0; place < SongListLayout.Places; place++)
        {
            var lit = 0;
            var unlit = 0;
            for (var bar = 0; bar < 3; bar++)
            {
                var rect = SongListLayout.Bar(row, place, bar).On(image);
                if (Colors.Fraction(image, rect, ColorClass.BarLit) >= MinimumBarShare)
                {
                    if (unlit > 0)
                        return null; // a lit bar after an unlit one: no mark looks like that
                    lit++;
                }
                else if (Colors.Fraction(image, rect, ColorClass.BarUnlit) >= MinimumBarShare)
                {
                    unlit++;
                }
            }

            if (lit + unlit == 0)
            {
                if (marks.Count > 0)
                    return null;
                continue;
            }

            if (lit + unlit < 3)
                return null;
            marks.Add(lit);
            if (lit < 3)
                continue;
            var level = NumberFieldReader.Read(image, SongListLayout.PlaceDigits(row, place).On(image), MaskKind.Light, TemplateFamilies.ListLevel,
                _templates);
            if (!level.IsClean || level.Value is not (>= 1 and <= 29))
                return null;
            perfectGames.Add(level.Value.Value);
        }

        return (marks, perfectGames);
    }
}

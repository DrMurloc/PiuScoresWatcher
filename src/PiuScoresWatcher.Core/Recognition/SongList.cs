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
///     apart when their panels read the same. A list whose lit chart can't be placed has no chart type, and nothing
///     else is read (D72).
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
    ulong Jacket);

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
///     always come from the best-score play.
/// </summary>
public sealed class SongListReader
{
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
            return new SongListReading(SongListStatus.Unplaced, unplaced.Reason, null, null, null, null, [], 0);
        if (sighting is not SongListSighting.Lit lit)
            return null;
        var titles = SongListLayout.Titles(image);
        var jacket = Colors.LuminancePrint(image, SongListLayout.LitJacket.On(image));
        var level = NumberFieldReader.Read(image, SongListLayout.BoxDigits(lit.Box).On(image), MaskKind.Light, TemplateFamilies.ListLevel, _templates);
        var score = NumberFieldReader.Read(image, SongListLayout.Score.On(image), MaskKind.Light, TemplateFamilies.ListValue, _templates);

        if (score.IsEmpty)
            return new SongListReading(SongListStatus.NoBest, null, lit.ChartType, level.Value, null, null, titles, jacket);
        if (!level.IsClean || level.Value is not (>= 1 and <= 29))
            return new SongListReading(SongListStatus.Unreadable, $"level read as '{level.Text}'", lit.ChartType, null, score.Value, null, titles,
                jacket);
        if (!score.IsClean || score.Value is not (>= 0 and <= 1_000_000))
            return new SongListReading(SongListStatus.Unreadable, $"best score read as '{score.Text}'", lit.ChartType, level.Value, null, null,
                titles, jacket);

        var grade = _grades.Classify(image, SongListLayout.Badge.On(image));
        if (!grade.IsConfident)
            return new SongListReading(SongListStatus.Unreadable,
                $"the grade badge matched no grade clearly ({grade.Grade} at {grade.Similarity:0.00}, ahead by {grade.Margin:0.00})",
                lit.ChartType, level.Value, score.Value, null, titles, jacket);

        var earned = Grades.Of(RiseMix.Rise, score.Value.Value);
        return earned == grade.Grade
            ? new SongListReading(SongListStatus.Best, null, lit.ChartType, level.Value, score.Value, grade.Grade, titles, jacket)
            : new SongListReading(SongListStatus.GradeDisagrees, $"the badge shows {grade.Grade}, {score.Value} earns {earned}",
                lit.ChartType, level.Value, score.Value, grade.Grade, titles, jacket);
    }
}

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>The two result-screen layouts RISE draws, and which PIU Scores mix each belongs to.</summary>
public enum ResultLayout
{
    /// <summary>Warm Up, Division: the "DANCE GRADE" screen — the mix <c>rise</c>.</summary>
    DanceGrade,

    /// <summary>The Arcade Station: the arcade's own result screen — the mix <c>riseArcade</c>.</summary>
    Arcade
}

/// <summary>Where everything sits on one layout, as fractions of the frame, and the fonts it prints in.</summary>
internal sealed record LayoutSpec(
    ResultLayout Layout,
    FractionRect[] Labels,
    FractionRect[] Values,
    FractionRect Accuracy,
    FractionRect Score,
    FractionRect Badge,
    FractionRect BadgeDigits,
    FractionRect Title,
    FractionRect? Sticker,
    string DigitFamily,
    string ScoreFamily,
    string LevelFamily,
    MaskKind ScoreInk);

/// <summary>Measured on the owner's 1080p screens (tools/reader-lab); the 720p screens confirmed the fractions scale.</summary>
internal static class Layouts
{
    /// <summary>The order of <see cref="LayoutSpec.Labels" /> and the first five of <see cref="LayoutSpec.Values" />.</summary>
    public static readonly ColorClass[] LabelColors =
    [
        ColorClass.Blue, ColorClass.Green, ColorClass.Yellow, ColorClass.Magenta, ColorClass.Red
    ];

    public static readonly string[] ValueNames = ["perfects", "greats", "goods", "bads", "misses", "max combo"];

    public static readonly LayoutSpec DanceGrade = new(
        ResultLayout.DanceGrade,
        Labels:
        [
            FractionRect.At1080p(940, 198, 1090, 236), FractionRect.At1080p(940, 296, 1060, 332),
            FractionRect.At1080p(940, 393, 1050, 430), FractionRect.At1080p(940, 490, 1020, 528),
            FractionRect.At1080p(940, 588, 1035, 626)
        ],
        Values:
        [
            FractionRect.At1080p(1690, 191, 1850, 239), FractionRect.At1080p(1690, 289, 1850, 337),
            FractionRect.At1080p(1690, 386, 1850, 434), FractionRect.At1080p(1690, 484, 1850, 532),
            FractionRect.At1080p(1690, 582, 1850, 630), FractionRect.At1080p(1690, 681, 1850, 729)
        ],
        Accuracy: FractionRect.At1080p(1690, 779, 1850, 827),
        Score: FractionRect.At1080p(300, 645, 740, 735),
        Badge: FractionRect.At1080p(1795, 32, 1868, 120),
        BadgeDigits: FractionRect.At1080p(1805, 40, 1859, 90),
        Title: FractionRect.At1080p(845, 38, 1785, 82),
        Sticker: FractionRect.At1080p(280, 240, 640, 530),
        DigitFamily: TemplateFamilies.DanceDigits,
        ScoreFamily: TemplateFamilies.DanceScore,
        LevelFamily: TemplateFamilies.DanceDigits,
        ScoreInk: MaskKind.Gold);

    public static readonly LayoutSpec Arcade = new(
        ResultLayout.Arcade,
        Labels:
        [
            FractionRect.At1080p(868, 442, 1052, 478), FractionRect.At1080p(890, 490, 1032, 524),
            FractionRect.At1080p(895, 537, 1022, 571), FractionRect.At1080p(915, 585, 1008, 619),
            FractionRect.At1080p(905, 632, 1018, 666)
        ],
        Values:
        [
            FractionRect.At1080p(596, 438, 700, 482), FractionRect.At1080p(596, 485, 700, 529),
            FractionRect.At1080p(596, 532, 700, 576), FractionRect.At1080p(596, 579, 700, 623),
            FractionRect.At1080p(596, 626, 700, 670), FractionRect.At1080p(596, 673, 700, 717)
        ],
        Accuracy: FractionRect.At1080p(596, 721, 720, 765),
        Score: FractionRect.At1080p(145, 352, 565, 428),
        Badge: FractionRect.At1080p(588, 285, 718, 402),
        BadgeDigits: FractionRect.At1080p(598, 322, 708, 388),
        Title: FractionRect.At1080p(560, 190, 1360, 248),
        Sticker: null,
        DigitFamily: TemplateFamilies.ArcadeDigits,
        ScoreFamily: TemplateFamilies.ArcadeScore,
        LevelFamily: TemplateFamilies.ArcadeLevel,
        ScoreInk: MaskKind.White);

    public static LayoutSpec For(ResultLayout layout)
    {
        return layout switch
        {
            ResultLayout.DanceGrade => DanceGrade,
            ResultLayout.Arcade => Arcade,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Not a result layout.")
        };
    }
}

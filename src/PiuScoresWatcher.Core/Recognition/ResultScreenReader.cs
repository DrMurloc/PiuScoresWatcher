using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Exceptions;

namespace PiuScoresWatcher.Core.Recognition;

public enum ReadingStatus
{
    /// <summary>Every number read; the checksum decides whether it is a play worth posting.</summary>
    Complete,

    /// <summary>A result screen whose numbers have not landed yet — the grade sticker shows first. Try the next frame.</summary>
    NumbersNotShown,

    /// <summary>A result layout that is not one play: a Challenge aggregate, which has no level badge.</summary>
    NotAPlay,

    /// <summary>A glyph no template claims; the frame is kept for the developer and nothing is posted.</summary>
    Unreadable
}

/// <summary>Everything the reader made of one frame. The title is not here: an adapter reads it from <see cref="Titles" />.</summary>
[ExcludeFromCodeCoverage]
public sealed record ResultScreenReading(
    ReadingStatus Status,
    string? Reason,
    ResultLayout Layout,
    RiseMix Mix,
    ChartType? ChartType,
    int? Level,
    Judgments? Judgments,
    int? MaxCombo,
    int? Score,
    string? AccuracyDigits,
    bool IsBroken,
    PixelRect TitleRegion,
    double LowestGlyphScore)
{
    /// <summary>The title's one place on a result screen, which never scrolls.</summary>
    public IReadOnlyList<TitleBox> Titles => [TitleBox.ResultTitle(TitleRegion)];
}

/// <summary>
///     Reads a result screen whose layout the detector named: the chart type from the badge's
///     colour, the level, the five judgments, max combo, score and accuracy by template matching,
///     and whether the grade went grey. It reads what is there and refuses what it cannot; the
///     checksum in <c>Scoring</c> decides whether the numbers agree with each other.
/// </summary>
public sealed class ResultScreenReader
{
    /// <summary>A level badge or a stepball shows one colour in at least this share of its saturated pixels.</summary>
    private const double MinimumBadgeShare = 0.4;

    /// <summary>A coloured grade sticker keeps far more of its bright pixels saturated than this; a grey one keeps none.</summary>
    private const double GreySticker = 0.1;

    private readonly TemplateSet _templates;

    public ResultScreenReader()
    {
        _templates = TemplateSet.Embedded;
    }

    internal ResultScreenReader(TemplateSet templates)
    {
        _templates = templates;
    }

    public ResultScreenReading Read(ScreenImage image, ResultLayout layout)
    {
        var spec = Layouts.For(layout);
        var mix = layout == ResultLayout.Arcade ? RiseMix.RiseArcade : RiseMix.Rise;
        var title = spec.Title.On(image);

        var chartType = ChartTypeOf(image, spec);
        if (chartType is null)
            return new ResultScreenReading(ReadingStatus.NotAPlay, "no level badge: a Challenge aggregate, not one play",
                layout, mix, null, null, null, null, null, null, false, title, 0);

        var values = new FieldReading[spec.Values.Length];
        for (var i = 0; i < values.Length; i++)
            values[i] = NumberFieldReader.Read(image, spec.Values[i].On(image), MaskKind.White, spec.DigitFamily, _templates);
        var score = NumberFieldReader.Read(image, spec.Score.On(image), spec.ScoreInk, spec.ScoreFamily, _templates);
        var level = NumberFieldReader.Read(image, spec.BadgeDigits.On(image), MaskKind.White, spec.LevelFamily, _templates);
        var accuracy = NumberFieldReader.Read(image, spec.Accuracy.On(image), MaskKind.White, spec.DigitFamily, _templates);

        if (values.Any(v => v.IsEmpty) || score.IsEmpty)
            return new ResultScreenReading(ReadingStatus.NumbersNotShown, "the numbers have not landed yet",
                layout, mix, chartType, null, null, null, null, null, false, title, 0);

        var lowest = values.Min(v => v.LowestScore);
        lowest = Math.Min(lowest, Math.Min(score.LowestScore, level.LowestScore));
        var unreadable = Unreadable(values, score, level);
        if (unreadable is not null)
            return new ResultScreenReading(ReadingStatus.Unreadable, unreadable,
                layout, mix, chartType, level.Value, null, values[5].Value, score.Value, accuracy.Digits, false, title, lowest);

        Judgments judgments;
        try
        {
            judgments = Judgments.From(values[0].Value!.Value, values[1].Value!.Value, values[2].Value!.Value,
                values[3].Value!.Value, values[4].Value!.Value);
        }
        catch (InvalidJudgmentsException refusal)
        {
            return new ResultScreenReading(ReadingStatus.Unreadable, refusal.Message,
                layout, mix, chartType, level.Value, null, values[5].Value, score.Value, accuracy.Digits, false, title, lowest);
        }

        var broken = spec.Sticker is { } sticker
                     && Colors.SaturatedShareOfBright(image, sticker.On(image)) < GreySticker;

        return new ResultScreenReading(ReadingStatus.Complete, null, layout, mix, chartType, level.Value, judgments,
            values[5].Value, score.Value, accuracy.Digits, broken, title, lowest);
    }

    /// <summary>The badge's colour says the chart type: red for singles on both layouts, blue for the half-double, green for the Arcade Station's double.</summary>
    private static ChartType? ChartTypeOf(ScreenImage image, LayoutSpec spec)
    {
        var (red, blue, green) = Colors.SaturatedHues(image, spec.Badge.On(image));
        if (red >= MinimumBadgeShare)
            return ChartType.Single;
        if (spec.Layout == ResultLayout.DanceGrade && blue >= MinimumBadgeShare)
            return ChartType.HalfDouble;
        if (spec.Layout == ResultLayout.Arcade && green >= MinimumBadgeShare)
            return ChartType.Double;
        return null;
    }

    /// <summary>
    ///     The first field that is not a number: a glyph no template claimed, or one that is not a digit — a '%' or a
    ///     '.' from the accuracy's own font can land in a count, and a count must parse, not throw.
    /// </summary>
    private static string? Unreadable(FieldReading[] values, FieldReading score, FieldReading level)
    {
        for (var i = 0; i < values.Length; i++)
            if (values[i].Value is null)
                return $"{Layouts.ValueNames[i]} read as '{values[i].Text}'";
        if (score.Value is null)
            return $"score read as '{score.Text}'";
        if (level.Value is null)
            return $"level read as '{level.Text}'";
        return null;
    }
}

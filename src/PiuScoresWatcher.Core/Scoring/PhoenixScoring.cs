using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Scoring;

/// <summary>
///     The Phoenix score, the rule RISE scores by on every station — copied from PIU Scores
///     (ScoreTracker.SharedKernel, <c>ScoreScreen.CalculatePhoenixScore</c>) and pinned here by the
///     owner's result screens, so a misread can be refused before it is posted. The server
///     recomputes it again and is the authority.
///     <para>
///         score = floor(995,000 × accuracy + 5,000 × maxCombo ⁄ notes), where
///         accuracy = (perfects + 0.6 greats + 0.2 goods + 0.1 bads) ⁄ notes. Kept in integers:
///         the weighted sum is 10P + 6G + 2Gd + B, so 995,000 × accuracy = 99,500 × weighted ⁄ notes.
///     </para>
/// </summary>
public static class PhoenixScoring
{
    public static int Score(Judgments judgments, int maxCombo)
    {
        if (maxCombo < 0 || maxCombo > judgments.Notes)
            throw new ArgumentOutOfRangeException(nameof(maxCombo), maxCombo, "Max combo lies between 0 and the note count.");
        var numerator = 99_500L * Weighted(judgments) + 5_000L * maxCombo;
        return (int)(numerator / judgments.Notes);
    }

    /// <summary>The accuracy as the screen prints it — a percentage truncated, never rounded, to two decimals.</summary>
    public static string AccuracyShown(Judgments judgments)
    {
        var hundredths = Weighted(judgments) * 1_000L / judgments.Notes;
        return $"{hundredths / 100}.{hundredths % 100:D2}";
    }

    /// <summary>The digits of <see cref="AccuracyShown" /> with the point removed, the form the reader compares.</summary>
    public static string AccuracyDigits(Judgments judgments)
    {
        return AccuracyShown(judgments).Replace(".", "", StringComparison.Ordinal);
    }

    private static long Weighted(Judgments j)
    {
        return 10L * j.Perfects + 6L * j.Greats + 2L * j.Goods + j.Bads;
    }
}

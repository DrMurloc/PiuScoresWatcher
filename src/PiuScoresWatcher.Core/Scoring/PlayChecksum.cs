using System.Globalization;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Core.Scoring;

/// <summary>What the checksum made of a reading; <see cref="Problem" /> is written to be shown.</summary>
[ExcludeFromCodeCoverage]
public sealed record ChecksumVerdict(bool Reconciles, int ExpectedScore, string ExpectedAccuracy, string? Problem);

/// <summary>
///     The local half of the two checksums: the five judgments and max combo must recompute to the
///     score on screen, and the accuracy the screen shows must begin with the digits they make. A
///     misread digit almost never survives both, so a bad read is refused here and never posted;
///     the server runs the same arithmetic again and is the authority.
/// </summary>
public static class PlayChecksum
{
    public static ChecksumVerdict Verify(ResultScreenReading reading)
    {
        if (reading.Status != ReadingStatus.Complete || reading.Judgments is not { } judgments
                                                      || reading.MaxCombo is not { } maxCombo || reading.Score is not { } score)
            return new ChecksumVerdict(false, 0, "", "the reading is not complete");

        int expected;
        try
        {
            expected = PhoenixScoring.Score(judgments, maxCombo);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new ChecksumVerdict(false, 0, "", $"max combo {maxCombo} exceeds the {judgments.Notes} notes");
        }

        var accuracy = PhoenixScoring.AccuracyShown(judgments);
        if (score != expected)
            return new ChecksumVerdict(false, expected, accuracy, $"the judgments make {expected}, the screen shows {score}");

        if (!AccuracyAgrees(reading.AccuracyDigits, accuracy))
            return new ChecksumVerdict(false, expected, accuracy, $"the accuracy reads {reading.AccuracyDigits}, the judgments make {accuracy}");

        return new ChecksumVerdict(true, expected, accuracy, null);
    }

    /// <summary>
    ///     The accuracy as read must land within a hundredth of the one the judgments make. A
    ///     hundredth of slack, because the game truncates a floating-point value: PARADOXX's
    ///     1452.6 / 1500 is exactly 96.84 and the screen prints 96.83. The read digits carry no
    ///     point (a '.' is too small to survive segmentation) and may trail a split '%', so the
    ///     computed value says how many digits the integer part has.
    /// </summary>
    private static bool AccuracyAgrees(string? readDigits, string computed)
    {
        if (string.IsNullOrEmpty(readDigits))
            return true; // nothing legible: the score alone decides
        var computedDigits = computed.Replace(".", "", StringComparison.Ordinal);
        if (readDigits.Length < computedDigits.Length)
            return false;
        var read = int.Parse(readDigits[..computedDigits.Length], CultureInfo.InvariantCulture);
        var expected = int.Parse(computedDigits, CultureInfo.InvariantCulture);
        return Math.Abs(read - expected) <= 1;
    }
}

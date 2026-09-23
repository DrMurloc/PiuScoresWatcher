using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Scoring;

/// <summary>
///     The letter grade a score earns, as the game prints it — copied from PIU Scores
///     (ScoreTracker.SharedKernel, <c>PhoenixLetterGrade</c>'s <c>RiseFloors</c> and
///     <c>Phoenix2Floors</c>) and pinned by the owner's result screens. Only a notification shows it;
///     nothing is posted from it (D41).
/// </summary>
public static class Grades
{
    /// <summary>RISE mode: nine grades, no plus tiers, no AAA.</summary>
    private static readonly (int Floor, string Grade)[] Rise =
    [
        (990_000, "SSS"), (970_000, "SS"), (950_000, "S"), (900_000, "AA"), (750_000, "A"),
        (650_000, "B"), (550_000, "C"), (500_000, "D"), (0, "F")
    ];

    /// <summary>The Arcade Station grades on Phoenix 2's floors.</summary>
    private static readonly (int Floor, string Grade)[] Phoenix2 =
    [
        (995_000, "SSS+"), (990_000, "SSS"), (985_000, "SS+"), (980_000, "SS"), (975_000, "S+"), (970_000, "S"),
        (960_000, "AAA+"), (950_000, "AAA"), (940_000, "AA+"), (920_000, "AA"), (900_000, "A+"), (800_000, "A"),
        (700_000, "B"), (600_000, "C"), (500_000, "D"), (0, "F")
    ];

    public static string Of(RiseMix mix, int score)
    {
        var ladder = mix switch
        {
            RiseMix.Rise => Rise,
            RiseMix.RiseArcade => Phoenix2,
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "Not a RISE mix.")
        };
        return ladder.First(step => score >= step.Floor).Grade;
    }
}

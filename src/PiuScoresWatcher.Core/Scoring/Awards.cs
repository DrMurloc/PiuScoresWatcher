using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Scoring;

/// <summary>The Phoenix plates; RISE mode hands out three of them under its own names.</summary>
public enum Award
{
    PerfectGame,
    UltimateGame,
    ExtremeGame,
    SuperbGame,
    MarvelousGame,
    TalentedGame,
    FairGame,
    RoughGame
}

/// <summary>
///     The award a play's judgments earn — copied from PIU Scores (ScoreTracker.SharedKernel,
///     <c>PhoenixPlateHelperMethods.Tolerances</c> and <c>AwardSets</c>, the same derivation its plays
///     endpoint runs). The first tolerance the judgments satisfy wins. RISE mode hands out Perfect
///     Game, Ultimate Game (shown as Full Combo) and Superb Game (shown as No Miss), so an Extreme Game
///     there is a No Miss and anything below is no mark at all. A broken play carries none (D41).
/// </summary>
public static class Awards
{
    public static Award? Of(RiseMix mix, Judgments judgments, bool isBroken)
    {
        if (isBroken)
            return null;
        var plate = Plate(judgments);
        return mix switch
        {
            RiseMix.RiseArcade => plate,
            RiseMix.Rise => plate switch
            {
                Award.PerfectGame => Award.PerfectGame,
                Award.UltimateGame => Award.UltimateGame,
                Award.ExtremeGame or Award.SuperbGame => Award.SuperbGame,
                _ => null
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "Not a RISE mix.")
        };
    }

    private static Award Plate(Judgments j)
    {
        if (j.Greats + j.Goods + j.Bads + j.Misses == 0)
            return Award.PerfectGame;
        if (j.Goods + j.Bads + j.Misses == 0)
            return Award.UltimateGame;
        if (j.Bads + j.Misses == 0)
            return Award.ExtremeGame;
        return j.Misses switch
        {
            0 => Award.SuperbGame,
            <= 5 => Award.MarvelousGame,
            <= 10 => Award.TalentedGame,
            <= 20 => Award.FairGame,
            _ => Award.RoughGame
        };
    }
}

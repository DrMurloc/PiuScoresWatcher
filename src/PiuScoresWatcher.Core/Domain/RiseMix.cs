namespace PiuScoresWatcher.Core.Domain;

/// <summary>
///     The two PIU Scores mixes a RISE result screen can belong to: RISE mode (Warm Up, Division)
///     and the Arcade Station. Records never cross between them, and the layout says which is which.
/// </summary>
public enum RiseMix
{
    Rise,
    RiseArcade
}

public static class RiseMixNames
{
    /// <summary>The value the PIU Scores v2 API takes for <c>mix</c>.</summary>
    public static string ApiName(this RiseMix mix)
    {
        return mix switch
        {
            RiseMix.Rise => "rise",
            RiseMix.RiseArcade => "riseArcade",
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "Not a RISE mix.")
        };
    }
}

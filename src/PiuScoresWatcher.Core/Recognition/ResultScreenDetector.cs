namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     Is this frame a result screen, and which layout? The five judgment labels — PERFECT in blue,
///     GREAT in green, GOOD in yellow, BAD in magenta, MISS in red — sit at fixed places on each
///     layout, and nothing else on any screen puts those five colours there. A frame is a result
///     screen when every one of them shows.
/// </summary>
public sealed class ResultScreenDetector
{
    /// <summary>The fixture screens score 0.36 and up on every label; anything else scores zero.</summary>
    public const double MinimumLabelShare = 0.15;

    public ResultLayout? Detect(ScreenImage image)
    {
        ResultLayout? best = null;
        var bestWeakest = 0.0;
        foreach (var spec in new[] { Layouts.DanceGrade, Layouts.Arcade })
        {
            var weakest = 1.0;
            for (var i = 0; i < spec.Labels.Length; i++)
                weakest = Math.Min(weakest, Colors.Fraction(image, spec.Labels[i].On(image), Layouts.LabelColors[i]));
            if (weakest >= MinimumLabelShare && weakest > bestWeakest)
            {
                best = spec.Layout;
                bestWeakest = weakest;
            }
        }

        return best;
    }
}

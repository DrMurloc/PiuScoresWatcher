using System.Text.Json;

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>The fonts a result screen prints numbers in; each is a family of glyph templates.</summary>
internal static class TemplateFamilies
{
    /// <summary>DANCE GRADE: judgments, max combo, accuracy, the level badge — the UI font.</summary>
    public const string DanceDigits = "dg";

    /// <summary>DANCE GRADE: the gold score on the brush stroke (the game's MainScore sprites).</summary>
    public const string DanceScore = "dgscore";

    /// <summary>Arcade Station: judgments, max combo, accuracy.</summary>
    public const string ArcadeDigits = "ar";

    /// <summary>Arcade Station: the seven-digit score (the game's Score_Number sprites).</summary>
    public const string ArcadeScore = "arscore";

    /// <summary>Arcade Station: the level on the stepball (the game's BigNumber sprites).</summary>
    public const string ArcadeLevel = "arlevel";

    /// <summary>Warm Up's song list: the digits in the level boxes, lit or not.</summary>
    public const string ListLevel = "wllevel";

    /// <summary>Warm Up's song list: the best score and the max combo beside it.</summary>
    public const string ListValue = "wlvalue";
}

internal sealed record Template(char Char, ulong[] Bits, double Aspect);

/// <summary>
///     The glyph templates, learned from the owner's screens and the game's own number sprites by
///     <c>tools/reader-lab</c> and embedded as <c>templates.json</c>. A glyph is the character of
///     its most similar template of a compatible shape; below <see cref="Acceptance" /> it is unread.
/// </summary>
internal sealed class TemplateSet
{
    /// <summary>The lowest agreement a correct read has shown across the fixture screens, with a little room.</summary>
    public const double Acceptance = 0.55;

    private static readonly Lazy<TemplateSet> EmbeddedSet = new(LoadEmbedded);
    private readonly Dictionary<string, List<Template>> _families;

    private TemplateSet(Dictionary<string, List<Template>> families)
    {
        _families = families;
    }

    public static TemplateSet Embedded => EmbeddedSet.Value;

    public int Count(string family)
    {
        return _families.TryGetValue(family, out var templates) ? templates.Count : 0;
    }

    public (char? Char, double Score) Classify(string family, ulong[] bits, double aspect)
    {
        if (!_families.TryGetValue(family, out var templates))
            return (null, 0);
        char? best = null;
        var bestScore = 0.0;
        foreach (var template in templates)
        {
            // a '1' is narrow and an '8' is not: a template of the wrong shape never wins by accident
            if (Math.Abs(template.Aspect - aspect) > 0.45 * Math.Max(template.Aspect, aspect))
                continue;
            var score = GlyphBits.Similarity(bits, template.Bits);
            if (score > bestScore)
            {
                best = template.Char;
                bestScore = score;
            }
        }

        return (best, bestScore);
    }

    public static TemplateSet Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("w").GetInt32() != GlyphBits.Width || root.GetProperty("h").GetInt32() != GlyphBits.Height)
            throw new InvalidDataException("The template file was built for another glyph grid.");
        var families = new Dictionary<string, List<Template>>(StringComparer.Ordinal);
        foreach (var family in root.GetProperty("families").EnumerateObject())
        {
            var templates = new List<Template>();
            foreach (var character in family.Value.EnumerateObject())
            foreach (var entry in character.Value.EnumerateArray())
                templates.Add(new Template(character.Name[0], GlyphBits.Parse(entry.GetProperty("bits").GetString()!),
                    entry.GetProperty("aspect").GetDouble()));
            families[family.Name] = templates;
        }

        return new TemplateSet(families);
    }

    private static TemplateSet LoadEmbedded()
    {
        using var stream = typeof(TemplateSet).Assembly.GetManifestResourceStream("templates.json")
                           ?? throw new InvalidOperationException("templates.json is not embedded in Core.");
        return Parse(stream);
    }
}

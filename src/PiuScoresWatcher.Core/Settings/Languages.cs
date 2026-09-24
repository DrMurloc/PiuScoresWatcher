namespace PiuScoresWatcher.Core.Settings;

/// <summary>
///     The languages the watcher speaks (D58): PIU Scores' own, less Murloc, in the order the site's picker
///     lists them — copied from the site's <c>SupportedCultures</c>, never referenced. Machine Default (D59)
///     places Windows' preferred languages onto these the way the site places a browser's: an exact match,
///     else the language's default region, else the next preference, and English when nothing places.
/// </summary>
public static class Languages
{
    public const string English = "en-US";
    public const string SpanishMexico = "es-MX";
    public const string SpanishSpain = "es-ES";
    public const string Portuguese = "pt-BR";
    public const string Korean = "ko-KR";
    public const string Japanese = "ja-JP";
    public const string French = "fr-FR";
    public const string Italian = "it-IT";

    /// <summary>Every language, in the picker's order; English, the key language, first.</summary>
    public static readonly IReadOnlyList<string> All = [English, SpanishMexico, SpanishSpain, Portuguese, Korean, Japanese, French, Italian];

    /// <summary>
    ///     The region a bare language lands on, copied from the site's table: Spanish from anywhere but Mexico is
    ///     Spain's (the site's ruling, 2026-08-03), Portuguese from anywhere is Brazil's.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Regions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = English,
        ["es"] = SpanishSpain,
        ["pt"] = Portuguese,
        ["ko"] = Korean,
        ["ja"] = Japanese,
        ["fr"] = French,
        ["it"] = Italian
    };

    /// <summary>A stored choice in its canonical spelling; null — Machine Default — for nothing or a language the watcher does not speak.</summary>
    public static string? Normalize(string? code)
    {
        return All.FirstOrDefault(language => string.Equals(language, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The closest language for one BCP 47 tag (<c>es-CL</c>, <c>ko</c>, <c>zh-Hans-CN</c>), or null when none is close.</summary>
    public static string? Closest(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var trimmed = tag.Trim();
        if (Normalize(trimmed) is { } exact)
            return exact;
        var separator = trimmed.IndexOf('-');
        var language = separator < 0 ? trimmed : trimmed[..separator];
        return Regions.GetValueOrDefault(language);
    }

    /// <summary>What the watcher speaks: the player's choice, else the first of Windows' languages that places, else English.</summary>
    public static string Resolve(string? chosen, IEnumerable<string> machine)
    {
        return Normalize(chosen) ?? machine.Select(Closest).FirstOrDefault(language => language is not null) ?? English;
    }
}

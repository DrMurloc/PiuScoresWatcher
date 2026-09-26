using System.Globalization;
using System.Text;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Catalog;

/// <summary>What the chart list made of a read title (D49, D56, D70).</summary>
[ExcludeFromCodeCoverage]
public abstract record CatalogMatch
{
    /// <summary>
    ///     The chart, in the catalog's spelling — at the level the judgments fit when the note count says the
    ///     level was misread.
    /// </summary>
    public sealed record Found(CatalogChart Chart) : CatalogMatch;

    /// <summary>
    ///     The title names a chart at the level read, but that chart has <see cref="CatalogChart.NoteCount" />
    ///     notes, the judgments add up to <paramref name="Notes" />, and no other chart of the song has as many:
    ///     something on the screen was misread, and nothing is sent.
    /// </summary>
    public sealed record Contradicted(CatalogChart Chart, int Notes) : CatalogMatch;

    /// <summary>
    ///     The whole title names a song the list has, but not the chart the screen shows: the list lacks it, and the read
    ///     is not what went wrong (D70). What the OCR read goes to PIU Scores as read, as for a title that names nothing.
    /// </summary>
    public sealed record Unlisted(string SongName) : CatalogMatch;

    /// <summary>No chart fits the title; what the OCR read goes to PIU Scores as read, and the site decides.</summary>
    public sealed record NotFound : CatalogMatch;
}

/// <summary>
///     How a reading sat in its box (D68): whether it ran off the box's right edge, where the game cuts off a title too
///     long to fit, and the length past which a title may scroll through that box. A result screen's title never scrolls.
/// </summary>
public readonly record struct TitleShape(bool RunsOffRight, int? ScrollsPast)
{
    public static TitleShape Whole { get; } = new(false, null);
}

/// <summary>
///     A mix's charts as PIU Scores lists them, for turning what the OCR read into the catalog's own
///     spelling (D49). The candidates are the songs that have the chart the screen shows. Case, spacing and
///     punctuation never count, and the letters a title font confuses — O and 0, I, l and 1, S and 5 —
///     compare as one; after that, a near miss wins when it is the only near miss, and so does a title the
///     OCR read with a piece missing or extra (K.O.A : Alice In Wonderworld read as "Alice In Wonderworld",
///     %X read as "O/ox") when it is the only one that fits. A title under four letters matches exactly or not
///     at all: one letter off is every other short title (D67).
///     A title the game cut off at its box's edge is the start of a longer title, and names only the one title that
///     starts with it and goes on past it; a whole reading that could be the tail of a title long enough to scroll
///     through the box names nothing (D68).
///     Where PIU Scores knows a chart's note count — most of the Arcade Station's — the judgments are the
///     check on the level: the chart must have as many notes as they add up to, and when the level the
///     screen shows has not, the song's chart that has is the one that was played (D56).
/// </summary>
public sealed class SongCatalog
{
    /// <summary>A contained title must be at least this much of the whole, and this long, to count.</summary>
    private const double ContainedShare = 0.6;

    private const int ContainedLength = 4;

    /// <summary>A title shorter than this matches exactly or not at all (D67).</summary>
    private const int ShortTitle = 4;

    /// <summary>The start of a title cut off at its box's edge names it only once this much of it shows (D68).</summary>
    private const int ShortestStart = 6;

    private readonly Dictionary<(ChartType Type, int Level), List<(string Key, CatalogChart Chart)>> _byChart;
    private readonly Dictionary<(ChartType Type, int Notes), List<(string Key, CatalogChart Chart)>> _byNotes;
    private readonly List<(string Key, CatalogChart Chart)> _songs;

    public SongCatalog(IEnumerable<CatalogChart> charts)
    {
        var keyed = charts.Select(chart => (Key: Key(chart.SongName), Chart: chart)).ToList();
        _byChart = keyed
            .GroupBy(entry => (entry.Chart.ChartType, entry.Chart.Level))
            .ToDictionary(group => group.Key, group => group.ToList());
        _byNotes = keyed
            .Where(entry => entry.Chart.NoteCount is not null)
            .GroupBy(entry => (entry.Chart.ChartType, entry.Chart.NoteCount!.Value))
            .ToDictionary(group => group.Key, group => group.ToList());
        _songs = keyed.DistinctBy(entry => entry.Chart.SongName).ToList();
        Count = keyed.Count;
    }

    public int Count { get; }

    /// <summary>
    ///     The chart the read title names at the level read; with <paramref name="notes" />, the judgments'
    ///     total, the chart must also have that many notes where the catalog knows — or the song's chart that
    ///     does is found instead. <paramref name="shape" /> is how the reading sat in its box (D68).
    /// </summary>
    public CatalogMatch Match(string? readTitle, ChartType type, int level, int? notes = null, TitleShape shape = default)
    {
        var key = string.IsNullOrWhiteSpace(readTitle) ? "" : Key(readTitle);
        if (key.Length == 0)
            return new CatalogMatch.NotFound();

        var atLevel = _byChart.GetValueOrDefault((type, level)) ?? [];
        var named = shape.RunsOffRight ? Start(key, atLevel) : Best(key, atLevel);
        if (named is not null && !shape.RunsOffRight && CouldBeATail(key, named.SongName, atLevel, shape.ScrollsPast))
            named = null;
        if (named is not null)
        {
            if (notes is null || named.NoteCount is null || named.NoteCount == notes)
                return new CatalogMatch.Found(named);
            // the song is right and the level is not: the song's chart with that many notes is the one played
            var fits = _byNotes.TryGetValue((type, notes.Value), out var sameNotes)
                ? sameNotes.Where(entry => entry.Chart.SongName == named.SongName).Select(entry => entry.Chart).ToList()
                : [];
            return fits.Count == 1 ? new CatalogMatch.Found(fits[0]) : new CatalogMatch.Contradicted(named, notes.Value);
        }

        // what ran off its box is only the start of a title: nothing more can be said of it
        if (shape.RunsOffRight)
            return new CatalogMatch.NotFound();

        // No song at the level read has that title: the level may be the misread, and the note count says which chart.
        if (notes is not null && _byNotes.TryGetValue((type, notes.Value), out var byNotes) && Best(key, byNotes) is { } counted)
            return new CatalogMatch.Found(counted);

        // A whole title the list has at other charts: it is the list that lacks this one (D70).
        return !CouldBeATail(key, null, atLevel, shape.ScrollsPast) && Best(key, _songs) is { } listed
            ? new CatalogMatch.Unlisted(listed.SongName)
            : new CatalogMatch.NotFound();
    }

    /// <summary>The one candidate the key names — exactly, by a near miss, or with a piece missing or extra — or null.</summary>
    private static CatalogChart? Best(string key, IReadOnlyList<(string Key, CatalogChart Chart)> candidates)
    {
        var exact = candidates.Where(c => c.Key == key).Select(c => c.Chart).DistinctBy(c => c.SongName).ToList();
        if (exact.Count > 0)
            return exact.Count == 1 ? exact[0] : null;
        if (key.Length < ShortTitle)
            return null;

        // A misread letter or two: the closest candidate, when nothing else is as close.
        var allowed = Allowed(key);
        var near = candidates
            .Select(c => (c.Chart, Distance: Distance(c.Key, key)))
            .Where(c => c.Distance <= allowed)
            .OrderBy(c => c.Distance)
            .ToList();
        if (near.Count > 0)
            return near.Count == 1 || near[1].Distance > near[0].Distance ? near[0].Chart : null;

        // A piece the OCR dropped or added — a prefix of symbols, a stray mark — when one title alone fits.
        var contained = candidates
            .Where(c => Contains(c.Key, key))
            .Select(c => c.Chart)
            .DistinctBy(c => c.SongName)
            .ToList();
        return contained.Count == 1 ? contained[0] : null;
    }

    /// <summary>
    ///     The one candidate a title cut off at its box's edge is the start of (D68): its title begins with what showed —
    ///     less the last letter, which the edge may have cut in half, and a misread letter allowed — and goes on past it.
    /// </summary>
    private static CatalogChart? Start(string key, IReadOnlyList<(string Key, CatalogChart Chart)> candidates)
    {
        var shown = key[..^1];
        if (shown.Length < ShortestStart)
            return null;
        var allowed = Allowed(shown);
        var starts = candidates
            .Where(c => c.Key.Length > key.Length)
            .Select(c => (c.Chart, Distance: StartDistance(c.Key, shown)))
            .Where(c => c.Distance <= allowed)
            .OrderBy(c => c.Distance)
            .ToList();
        return starts.Count > 0 && (starts.Count == 1 || starts[1].Distance > starts[0].Distance) ? starts[0].Chart : null;
    }

    /// <summary>
    ///     Whether a whole reading could be the tail of another title at the chart, one long enough to scroll through the
    ///     box: "Pumping up" is how The People didn't know "Pumping up" leaves the panel (D68).
    /// </summary>
    private static bool CouldBeATail(string key, string? named, IReadOnlyList<(string Key, CatalogChart Chart)> atLevel, int? scrollsPast)
    {
        if (scrollsPast is not { } past)
            return false;
        var allowed = key.Length < ShortTitle ? 0 : Allowed(key);
        return atLevel.Any(c => c.Chart.SongName != named && c.Chart.SongName.Length > past && c.Key.Length > key.Length
                                && Distance(c.Key[^key.Length..], key) <= allowed);
    }

    /// <summary>The edits a near miss may take: a misread letter in every eight, and at least one.</summary>
    private static int Allowed(string key)
    {
        return Math.Max(1, key.Length / 8);
    }

    /// <summary>What showed against a title's first letters, a letter more or fewer.</summary>
    private static int StartDistance(string title, string shown)
    {
        var best = int.MaxValue;
        for (var length = shown.Length - 1; length <= shown.Length + 1; length++)
            if (length > 0 && length <= title.Length)
                best = Math.Min(best, Distance(title[..length], shown));
        return best;
    }

    private static bool Contains(string a, string b)
    {
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        return shorter.Length >= ContainedLength && shorter.Length >= longer.Length * ContainedShare
                                                 && longer.Contains(shorter, StringComparison.Ordinal);
    }

    /// <summary>A title reduced to what the font can be trusted to show: letters and digits, lower case, confusable glyphs folded.</summary>
    public static string Key(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var c in title.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            var folded = c switch
            {
                '0' => 'o',
                '1' or 'i' or '|' or '!' => 'l',
                '5' => 's',
                _ => c
            };
            if (char.IsLetterOrDigit(folded))
                builder.Append(folded);
        }

        return builder.ToString();
    }

    /// <summary>Edits (insert, delete, replace) from one key to the other.</summary>
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Count} charts");
    }
}

/// <summary>The chart lists the watcher has loaded, one per mix; null until one arrives, and the pipeline copes.</summary>
public interface ISongCatalogs
{
    SongCatalog? For(RiseMix mix);
}

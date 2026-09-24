using System.Globalization;
using System.Text;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Catalog;

/// <summary>What the chart list made of a read title (D49, D56).</summary>
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

    /// <summary>No chart fits the title; what the OCR read goes to PIU Scores as read, and the site decides.</summary>
    public sealed record NotFound : CatalogMatch;
}

/// <summary>
///     A mix's charts as PIU Scores lists them, for turning what the OCR read into the catalog's own
///     spelling (D49). The candidates are the songs that have the chart the screen shows. Case, spacing and
///     punctuation never count, and the letters a title font confuses — O and 0, I, l and 1, S and 5 —
///     compare as one; after that, a near miss wins when it is the only near miss, and so does a title the
///     OCR read with a piece missing or extra (K.O.A : Alice In Wonderworld read as "Alice In Wonderworld",
///     %X read as "O/ox") when it is the only one that fits.
///     Where PIU Scores knows a chart's note count — most of the Arcade Station's — the judgments are the
///     check on the level: the chart must have as many notes as they add up to, and when the level the
///     screen shows has not, the song's chart that has is the one that was played (D56).
/// </summary>
public sealed class SongCatalog
{
    /// <summary>A contained title must be at least this much of the whole, and this long, to count.</summary>
    private const double ContainedShare = 0.6;

    private const int ContainedLength = 4;

    private readonly Dictionary<(ChartType Type, int Level), List<(string Key, CatalogChart Chart)>> _byChart;
    private readonly Dictionary<(ChartType Type, int Notes), List<(string Key, CatalogChart Chart)>> _byNotes;

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
        Count = keyed.Count;
    }

    public int Count { get; }

    /// <summary>
    ///     The chart the read title names at the level read; with <paramref name="notes" />, the judgments'
    ///     total, the chart must also have that many notes where the catalog knows — or the song's chart that
    ///     does is found instead.
    /// </summary>
    public CatalogMatch Match(string? readTitle, ChartType type, int level, int? notes = null)
    {
        var key = string.IsNullOrWhiteSpace(readTitle) ? "" : Key(readTitle);
        if (key.Length == 0)
            return new CatalogMatch.NotFound();

        if (_byChart.TryGetValue((type, level), out var atLevel) && Best(key, atLevel) is { } named)
        {
            if (notes is null || named.NoteCount is null || named.NoteCount == notes)
                return new CatalogMatch.Found(named);
            // the song is right and the level is not: the song's chart with that many notes is the one played
            var fits = _byNotes.TryGetValue((type, notes.Value), out var sameNotes)
                ? sameNotes.Where(entry => entry.Chart.SongName == named.SongName).Select(entry => entry.Chart).ToList()
                : [];
            return fits.Count == 1 ? new CatalogMatch.Found(fits[0]) : new CatalogMatch.Contradicted(named, notes.Value);
        }

        // No song at the level read has that title: the level may be the misread, and the note count says which chart.
        if (notes is not null && _byNotes.TryGetValue((type, notes.Value), out var byNotes) && Best(key, byNotes) is { } counted)
            return new CatalogMatch.Found(counted);
        return new CatalogMatch.NotFound();
    }

    /// <summary>The one candidate the key names — exactly, by a near miss, or with a piece missing or extra — or null.</summary>
    private static CatalogChart? Best(string key, IReadOnlyList<(string Key, CatalogChart Chart)> candidates)
    {
        var exact = candidates.Where(c => c.Key == key).Select(c => c.Chart).DistinctBy(c => c.SongName).ToList();
        if (exact.Count > 0)
            return exact.Count == 1 ? exact[0] : null;

        // A misread letter or two: the closest candidate, when nothing else is as close.
        var allowed = Math.Max(1, key.Length / 8);
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

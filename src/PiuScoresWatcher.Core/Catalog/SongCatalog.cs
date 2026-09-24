using System.Globalization;
using System.Text;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Catalog;

/// <summary>
///     A mix's charts as PIU Scores lists them, for turning what the OCR read into the catalog's own
///     spelling (D49). Only songs that have the chart the screen shows are candidates. Case, spacing
///     and punctuation never count, and the letters a title font confuses — O and 0, I, l and 1, S
///     and 5 — compare as one; after that, a near miss wins when it is the only near miss.
/// </summary>
public sealed class SongCatalog
{
    private readonly Dictionary<(ChartType Type, int Level), List<(string Key, CatalogChart Chart)>> _byChart;

    public SongCatalog(IEnumerable<CatalogChart> charts)
    {
        _byChart = charts
            .GroupBy(chart => (chart.ChartType, chart.Level))
            .ToDictionary(group => group.Key, group => group.Select(chart => (Key(chart.SongName), chart)).ToList());
        Count = _byChart.Values.Sum(list => list.Count);
    }

    public int Count { get; }

    /// <summary>The chart the read title names, or null when no song with that chart matches it, or more than one does.</summary>
    public CatalogChart? Match(string? readTitle, ChartType type, int level)
    {
        if (string.IsNullOrWhiteSpace(readTitle) || !_byChart.TryGetValue((type, level), out var candidates))
            return null;
        var key = Key(readTitle);
        if (key.Length == 0)
            return null;

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
        if (near.Count == 0)
            return null;
        return near.Count == 1 || near[1].Distance > near[0].Distance ? near[0].Chart : null;
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

using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Core.Catalog;

/// <summary>
///     A title as read and what the chart list made of it. <see cref="Read" /> is the first reading, which stands when no
///     chart fits; <see cref="Seen" /> is each box's first reading, for the log and the kept screen's note.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TitleChoice(string? Read, CatalogMatch Match, IReadOnlyList<BoxReading> Seen)
{
    /// <summary>What was seen, for a note: <c>'wanna go to the moon palace' (list) / 'wanna go to the moon pali' (panel)</c>.</summary>
    public string Described => Seen.Count == 0 ? "nothing" : string.Join(" / ", Seen.Select(seen => $"'{seen.Read}' ({seen.Box})"));
}

/// <summary>The first reading a box gave.</summary>
[ExcludeFromCodeCoverage]
public sealed record BoxReading(string Box, string Read);

public static class Titles
{
    /// <summary>
    ///     Reads the title box by box, attempt by attempt, until a reading names a chart in <paramref name="catalog" />
    ///     (D55, D66); without a catalog the first reading stands. A null <see cref="TitleChoice.Read" /> means no attempt
    ///     read anything at all.
    /// </summary>
    public static async Task<TitleChoice> ChooseAsync(this ITitleReader titles, ScreenImage image, IReadOnlyList<TitleBox> boxes,
        SongCatalog? catalog, ChartType type, int level, int? notes, CancellationToken cancellationToken)
    {
        string? first = null;
        var seen = new List<BoxReading>();
        foreach (var box in boxes)
        {
            await foreach (var read in titles.ReadAsync(image, box, cancellationToken))
            {
                first ??= read;
                if (seen.Count == 0 || seen[^1].Box != box.Name)
                    seen.Add(new BoxReading(box.Name, read));
                if (catalog is null)
                    return new TitleChoice(first, new CatalogMatch.NotFound(), seen);
                var match = catalog.Match(read, type, level, notes);
                if (match is not CatalogMatch.NotFound)
                    return new TitleChoice(read, match, seen);
            }
        }

        return new TitleChoice(first, new CatalogMatch.NotFound(), seen);
    }
}

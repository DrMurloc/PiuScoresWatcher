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
    ///     (D55, D66), each reading taken as it sat in its box — cut off at the edge or whole (D68); without a catalog the
    ///     first reading stands. A song the list has only at other charts does not stop the search, since a later reading
    ///     may name the chart, but it is what the choice says when none does (D70). A null <see cref="TitleChoice.Read" />
    ///     means no attempt read anything at all.
    /// </summary>
    public static async Task<TitleChoice> ChooseAsync(this ITitleReader titles, ScreenImage image, IReadOnlyList<TitleBox> boxes,
        SongCatalog? catalog, ChartType type, int level, int? notes, CancellationToken cancellationToken)
    {
        string? first = null;
        CatalogMatch.Unlisted? unlisted = null;
        var seen = new List<BoxReading>();
        foreach (var box in boxes)
        {
            var shape = box.ScrollsPast is { } past ? new TitleShape(TitleInk.RunsOffRight(image, box.Region), past) : TitleShape.Whole;
            await foreach (var read in titles.ReadAsync(image, box, cancellationToken))
            {
                first ??= read;
                if (seen.Count == 0 || seen[^1].Box != box.Name)
                    seen.Add(new BoxReading(box.Name, read));
                if (catalog is null)
                    return new TitleChoice(first, new CatalogMatch.NotFound(), seen);
                switch (catalog.Match(read, type, level, notes, shape))
                {
                    case (CatalogMatch.Found or CatalogMatch.Contradicted) and var match:
                        return new TitleChoice(read, match, seen);
                    case CatalogMatch.Unlisted listed:
                        unlisted ??= listed;
                        break;
                }
            }
        }

        return new TitleChoice(first, unlisted ?? (CatalogMatch)new CatalogMatch.NotFound(), seen);
    }

    /// <summary>
    ///     A song list row's title (D75): read attempt by attempt until a reading names a chart at one of the row's
    ///     <paramref name="levels" />, each matched on its own among the songs that have that chart, the leftmost first. A row
    ///     is one song, so the chart found names the song for the row's other charts. Otherwise as
    ///     <see cref="ChooseAsync" />: a song the list has only at other charts is what the choice says when nothing is found.
    /// </summary>
    public static async Task<TitleChoice> ChooseSongAsync(this ITitleReader titles, ScreenImage image, TitleBox box, SongCatalog catalog,
        ChartType type, IReadOnlyList<int> levels, CancellationToken cancellationToken)
    {
        string? first = null;
        CatalogMatch.Unlisted? unlisted = null;
        var shape = box.ScrollsPast is { } past ? new TitleShape(TitleInk.RunsOffRight(image, box.Region), past) : TitleShape.Whole;
        await foreach (var read in titles.ReadAsync(image, box, cancellationToken))
        {
            first ??= read;
            foreach (var level in levels)
                switch (catalog.Match(read, type, level, null, shape))
                {
                    case CatalogMatch.Found found:
                        return new TitleChoice(read, found, [new BoxReading(box.Name, first)]);
                    case CatalogMatch.Unlisted listed:
                        unlisted ??= listed;
                        break;
                }
        }

        return new TitleChoice(first, unlisted ?? (CatalogMatch)new CatalogMatch.NotFound(), first is null ? [] : [new BoxReading(box.Name, first)]);
    }
}

using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Core.Catalog;

/// <summary>A title as read and what the chart list made of it; <see cref="Read" /> is the first reading, which stands when no chart fits.</summary>
[ExcludeFromCodeCoverage]
public sealed record TitleChoice(string? Read, CatalogMatch Match);

public static class Titles
{
    /// <summary>
    ///     Reads the title attempt by attempt until a reading names a chart in <paramref name="catalog" /> (D55);
    ///     without a catalog the first reading stands. A null <see cref="TitleChoice.Read" /> means no attempt
    ///     read anything at all.
    /// </summary>
    public static async Task<TitleChoice> ChooseAsync(this ITitleReader titles, ScreenImage image, PixelRect region, SongCatalog? catalog,
        ChartType type, int level, int? notes, CancellationToken cancellationToken)
    {
        string? first = null;
        await foreach (var read in titles.ReadAsync(image, region, cancellationToken))
        {
            first ??= read;
            if (catalog is null)
                break;
            var match = catalog.Match(read, type, level, notes);
            if (match is not CatalogMatch.NotFound)
                return new TitleChoice(read, match);
        }

        return new TitleChoice(first, new CatalogMatch.NotFound());
    }
}

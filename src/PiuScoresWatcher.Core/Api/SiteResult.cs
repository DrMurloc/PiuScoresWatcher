using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Api;

/// <summary>What a read from PIU Scores came back with: the value, a refused token, or why it failed.</summary>
[ExcludeFromCodeCoverage]
public abstract record SiteResult<T>
{
    public sealed record Ok(T Value) : SiteResult<T>;

    /// <summary>No token is stored, or the site refused the one that is.</summary>
    public sealed record Unauthorized : SiteResult<T>;

    public sealed record Failed(int? Status, string Message) : SiteResult<T>;
}

/// <summary>
///     One chart of a mix, as <c>GET api/v2/charts</c> lists it — what a read title is matched against (D49).
///     <paramref name="NoteCount" /> is what a full play's judgments add up to, when PIU Scores knows it: most of
///     the Arcade Station's charts, none of Warm Up's yet (D56).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CatalogChart(Guid Id, string SongName, ChartType ChartType, int Level, int? NoteCount = null);

/// <summary>The player's stored best on one chart, as <c>GET api/v2/players/{id}/scores</c> lists it.</summary>
[ExcludeFromCodeCoverage]
public sealed record StoredBest(Guid ChartId, int? Score, bool IsBroken);

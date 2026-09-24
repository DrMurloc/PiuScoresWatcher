using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Api;

/// <summary>
///     PIU Scores, as far as the watcher needs it: who the token is, one play at a time, and — for a
///     bulk capture and for matching titles — a mix's chart list and the player's own bests.
/// </summary>
public interface IPlaysClient
{
    Task<IdentityCheck> WhoAmIAsync(CancellationToken cancellationToken);

    /// <summary>Posts one play; <paramref name="source" /> is the API's <c>source</c>, as <see cref="CaptureSources" /> names it.</summary>
    Task<PostOutcome> PostAsync(ObservedPlay play, string source, CancellationToken cancellationToken);

    /// <summary>Every chart of the mix, page by page.</summary>
    Task<SiteResult<IReadOnlyList<CatalogChart>>> GetChartsAsync(RiseMix mix, CancellationToken cancellationToken);

    /// <summary>The player's stored bests on the mix, page by page.</summary>
    Task<SiteResult<IReadOnlyList<StoredBest>>> GetBestsAsync(Guid playerId, RiseMix mix, CancellationToken cancellationToken);
}

/// <summary>Where the personal token lives between launches; the App keeps it DPAPI-encrypted, never in the settings file.</summary>
public interface ITokenStore
{
    string? Load();

    void Save(string token);

    void Clear();
}

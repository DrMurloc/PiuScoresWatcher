using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Api;

/// <summary>
///     The calls the watcher makes, on the wire shape PIU Scores' <c>docs/API.md</c> and Swagger
///     publish: <c>GET api/v2/players/me</c>, <c>POST api/v2/players/me/plays</c>, and for a bulk
///     capture <c>GET api/v2/charts</c> and <c>GET api/v2/players/{id}/scores</c>, with the personal
///     token as the Basic password (the username is not read). The <see cref="HttpClient" /> is the
///     App's — its base address is the site, production unless a dev switch says otherwise.
/// </summary>
public sealed class PiuScoresClient(HttpClient http, ITokenStore tokens) : IPlaysClient
{
    private const string ProblemTypeBase = "https://piuscores.arroweclip.se/errors/";

    /// <summary>The largest page the collections serve; a mix's charts arrive in a handful of requests.</summary>
    private const int PageSize = 500;

    /// <summary>A collection longer than this many pages is a server fault, not a catalog.</summary>
    private const int MaxPages = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IdentityCheck> WhoAmIAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/players/me");
        if (!Authorize(request))
            return new IdentityCheck.Unauthorized();
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return new IdentityCheck.Unauthorized();
            if (!response.IsSuccessStatusCode)
                return new IdentityCheck.Failed((int)response.StatusCode, await ProblemSummary(response, cancellationToken));
            var player = await response.Content.ReadFromJsonAsync<PlayerJson>(Json, cancellationToken);
            return player is null || player.UserId == Guid.Empty || string.IsNullOrWhiteSpace(player.Username)
                ? new IdentityCheck.Failed((int)response.StatusCode, "the player came back without an id or a name")
                : new IdentityCheck.Connected(new PlayerIdentity(player.UserId, player.Username, player.GameTag));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new IdentityCheck.Failed(null, failure.Message);
        }
    }

    public async Task<PostOutcome> PostAsync(ObservedPlay play, string source, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v2/players/me/plays");
        if (!Authorize(request))
            return new PostOutcome.NotConnected();
        var judgments = play.Judgments;
        request.Content = JsonContent.Create(new PlaysRequestJson(play.Mix.ApiName(), source,
        [
            new PlayJson(play.SongName, play.ChartType.ToString(), play.Level, judgments?.Perfects, judgments?.Greats,
                judgments?.Goods, judgments?.Bads, judgments?.Misses, play.MaxCombo, play.Score, play.IsBroken, play.PlayedAt)
        ]), options: Json);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return await Outcome(response, cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new PostOutcome.Failed(null, failure.Message);
        }
    }

    public async Task<SiteResult<IReadOnlyList<CatalogChart>>> GetChartsAsync(RiseMix mix, CancellationToken cancellationToken)
    {
        var charts = new List<CatalogChart>();
        var failure = await ReadPagesAsync<ChartPageJson>($"api/v2/charts?mix={mix.ApiName()}&limit={PageSize}", page =>
        {
            foreach (var chart in page.Data ?? [])
                if (chart.SongName is { Length: > 0 } song && Enum.TryParse<ChartType>(chart.Type, true, out var type))
                    charts.Add(new CatalogChart(chart.Id, song, type, chart.Level));
            return page.Next;
        }, cancellationToken);
        return failure is null ? new SiteResult<IReadOnlyList<CatalogChart>>.Ok(charts) : failure.As<IReadOnlyList<CatalogChart>>();
    }

    public async Task<SiteResult<IReadOnlyList<StoredBest>>> GetBestsAsync(Guid playerId, RiseMix mix, CancellationToken cancellationToken)
    {
        var bests = new List<StoredBest>();
        var failure = await ReadPagesAsync<ScorePageJson>($"api/v2/players/{playerId}/scores?mix={mix.ApiName()}&limit={PageSize}", page =>
        {
            foreach (var row in page.Data ?? [])
                bests.Add(new StoredBest(row.ChartId, row.Score, row.IsBroken));
            return page.Next;
        }, cancellationToken);
        return failure is null ? new SiteResult<IReadOnlyList<StoredBest>>.Ok(bests) : failure.As<IReadOnlyList<StoredBest>>();
    }

    /// <summary>
    ///     Follows a collection's <c>next</c> links to the end, handing each page to <paramref name="take" />,
    ///     which returns the next link. Null when every page arrived, else why not. A next link on
    ///     another host is refused rather than followed: the token goes to PIU Scores and nowhere else.
    /// </summary>
    private async Task<PageFailure?> ReadPagesAsync<TPage>(string first, Func<TPage, string?> take, CancellationToken cancellationToken)
        where TPage : class
    {
        string? next = first;
        for (var pages = 0; next is not null; pages++)
        {
            if (pages >= MaxPages)
                return new PageFailure(false, null, "the collection never ended");
            if (!OnThisSite(next))
                return new PageFailure(false, null, $"the next page is not on PIU Scores: {next}");
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            if (!Authorize(request))
                return new PageFailure(true, null, "no token is stored");
            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return new PageFailure(true, 401, "the token was not accepted");
                if (!response.IsSuccessStatusCode)
                    return new PageFailure(false, (int)response.StatusCode, await ProblemSummary(response, cancellationToken));
                var page = await response.Content.ReadFromJsonAsync<TPage>(Json, cancellationToken);
                if (page is null)
                    return new PageFailure(false, (int)response.StatusCode, "a page came back empty");
                next = take(page);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or JsonException)
            {
                return new PageFailure(false, null, failure.Message);
            }
        }

        return null;
    }

    private bool OnThisSite(string link)
    {
        if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri) || !uri.IsAbsoluteUri)
            return true;
        return http.BaseAddress is { } site
               && string.Equals(uri.Host, site.Host, StringComparison.OrdinalIgnoreCase)
               && uri.Scheme == site.Scheme && uri.Port == site.Port;
    }

    /// <summary>Why a collection read stopped; each caller turns it into its own result type.</summary>
    private sealed record PageFailure(bool Unauthorized, int? Status, string Message)
    {
        public SiteResult<T> As<T>()
        {
            return Unauthorized ? new SiteResult<T>.Unauthorized() : new SiteResult<T>.Failed(Status, Message);
        }
    }

    private bool Authorize(HttpRequestMessage request)
    {
        var token = tokens.Load();
        if (string.IsNullOrWhiteSpace(token))
            return false;
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"watcher:{token}")));
        return true;
    }

    private static async Task<PostOutcome> Outcome(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
            {
                var result = await response.Content.ReadFromJsonAsync<RecordedJson>(Json, cancellationToken);
                return result is null
                    ? new PostOutcome.Failed(200, "the response carried no result")
                    : new PostOutcome.Recorded(result.Recorded, result.Mix ?? "");
            }
            case HttpStatusCode.Unauthorized:
                return new PostOutcome.Unauthorized();
            case HttpStatusCode.TooManyRequests:
                return new PostOutcome.RateLimited(response.Headers.RetryAfter?.Delta);
            case HttpStatusCode.BadRequest:
            {
                var problem = await Problem(response, cancellationToken);
                return new PostOutcome.Refused(problem?.Slug ?? "unknown", problem?.Detail ?? problem?.Title);
            }
            case HttpStatusCode.NotFound:
            {
                var problem = await Problem(response, cancellationToken);
                return new PostOutcome.SongUnknown(problem?.Detail ?? problem?.Title);
            }
            default:
                return new PostOutcome.Failed((int)response.StatusCode, await ProblemSummary(response, cancellationToken));
        }
    }

    private static async Task<ProblemJson?> Problem(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemJson>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string> ProblemSummary(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await Problem(response, cancellationToken);
        return problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? "no reason given";
    }

    private sealed record PlaysRequestJson(string Mix, string Source, PlayJson[] Plays);

    /// <summary>The counts and the max combo are omitted, not zeroed, for a capture that has none (D46).</summary>
    private sealed record PlayJson(
        string SongName, string ChartType, int Level, int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses,
        int? MaxCombo, int Score, bool IsBroken, DateTimeOffset PlayedAt);

    private sealed record RecordedJson(int Recorded, string? Mix, string? ScoringModel);

    private sealed record PlayerJson(Guid UserId, string? Username, string? GameTag);

    private sealed record ChartJson(Guid Id, string? SongName, string? Type, int Level);

    private sealed record ChartPageJson(ChartJson[]? Data, string? Next);

    private sealed record ScoreJson(Guid ChartId, int? Score, bool IsBroken);

    private sealed record ScorePageJson(ScoreJson[]? Data, string? Next);

    private sealed record ProblemJson(string? Type, string? Title, int? Status, string? Detail)
    {
        /// <summary>The problem's slug — <c>judgments-do-not-reconcile</c> — off the site's <c>errors/</c> base.</summary>
        public string? Slug => Type is null ? null
            : Type.StartsWith(ProblemTypeBase, StringComparison.Ordinal) ? Type[ProblemTypeBase.Length..]
            : Type[(Type.LastIndexOf('/') + 1)..];
    }
}

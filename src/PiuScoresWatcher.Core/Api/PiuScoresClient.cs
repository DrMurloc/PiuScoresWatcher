using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Api;

/// <summary>
///     The two calls the watcher makes, on the wire shape PIU Scores' <c>docs/API.md</c> and Swagger
///     publish: <c>GET api/v2/players/me</c> and <c>POST api/v2/players/me/plays</c>, with the
///     personal token as the Basic password (the username is not read). The <see cref="HttpClient" />
///     is the App's — its base address is the site, production unless a dev switch says otherwise.
/// </summary>
public sealed class PiuScoresClient(HttpClient http, ITokenStore tokens) : IPlaysClient
{
    private const string ProblemTypeBase = "https://piuscores.arroweclip.se/errors/";

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

    public async Task<PostOutcome> PostAsync(ObservedPlay play, CaptureSource source, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v2/players/me/plays");
        if (!Authorize(request))
            return new PostOutcome.NotConnected();
        request.Content = JsonContent.Create(new PlaysRequestJson(play.Mix.ApiName(), source.Token(),
        [
            new PlayJson(play.SongName, play.ChartType.ToString(), play.Level, play.Judgments.Perfects, play.Judgments.Greats,
                play.Judgments.Goods, play.Judgments.Bads, play.Judgments.Misses, play.MaxCombo, play.Score, play.IsBroken, play.PlayedAt)
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

    private sealed record PlayJson(
        string SongName, string ChartType, int Level, int Perfects, int Greats, int Goods, int Bads, int Misses,
        int MaxCombo, int Score, bool IsBroken, DateTimeOffset PlayedAt);

    private sealed record RecordedJson(int Recorded, string? Mix, string? ScoringModel);

    private sealed record PlayerJson(Guid UserId, string? Username, string? GameTag);

    private sealed record ProblemJson(string? Type, string? Title, int? Status, string? Detail)
    {
        /// <summary>The problem's slug — <c>judgments-do-not-reconcile</c> — off the site's <c>errors/</c> base.</summary>
        public string? Slug => Type is null ? null
            : Type.StartsWith(ProblemTypeBase, StringComparison.Ordinal) ? Type[ProblemTypeBase.Length..]
            : Type[(Type.LastIndexOf('/') + 1)..];
    }
}

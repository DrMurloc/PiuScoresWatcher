using System.Net;
using System.Text;
using System.Text.Json;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Tests.ApiTests;

/// <summary>
///     The wire shape PIU Scores publishes, pinned from this side: the routes, the Basic token, the
///     body's fields, and every answer the server gives turned into an outcome.
/// </summary>
public sealed class PiuScoresClientTests
{
    private static readonly ObservedPlay Play = new(RiseMix.Rise, "Morrighan", ChartType.Single, 20,
        Judgments.From(911, 59, 13, 3, 14), 170, 945403, false, new DateTimeOffset(2026, 9, 22, 20, 13, 28, TimeSpan.FromHours(-4)));

    private sealed class StubTokens(string? token) : ITokenStore
    {
        public string? Load() => token;
        public void Save(string value) { }
        public void Clear() { }
    }

    private sealed class Exchange(HttpStatusCode status, string? body, string mediaType = "application/json", TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, Encoding.UTF8, mediaType);
            if (retryAfter is { } wait)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
            return response;
        }
    }

    private static (PiuScoresClient Client, Exchange Exchange) ClientOver(HttpStatusCode status, string? body, string? token = "pst_secret",
        string mediaType = "application/json", TimeSpan? retryAfter = null)
    {
        var exchange = new Exchange(status, body, mediaType, retryAfter);
        var http = new HttpClient(exchange) { BaseAddress = new Uri("https://piuscores.test/") };
        return (new PiuScoresClient(http, new StubTokens(token)), exchange);
    }

    [Fact]
    public async Task WhoAmIAsksForMeWithTheTokenAsTheBasicPassword()
    {
        var (client, exchange) = ClientOver(HttpStatusCode.OK,
            """{"userId":"11111111-1111-1111-1111-111111111111","username":"DrMurloc","gameTag":"MURLOC","isPublic":true}""");

        var check = await client.WhoAmIAsync(CancellationToken.None);

        Assert.Equal("https://piuscores.test/api/v2/players/me", exchange.Request!.RequestUri!.ToString());
        Assert.Equal("Basic", exchange.Request.Headers.Authorization!.Scheme);
        Assert.Equal("watcher:pst_secret", Encoding.UTF8.GetString(Convert.FromBase64String(exchange.Request.Headers.Authorization.Parameter!)));
        var connected = Assert.IsType<IdentityCheck.Connected>(check);
        Assert.Equal("DrMurloc", connected.Player.Username);
        Assert.Equal("MURLOC", connected.Player.GameTag);
    }

    [Fact]
    public async Task WithoutATokenNothingIsSent()
    {
        var (client, exchange) = ClientOver(HttpStatusCode.OK, "{}", token: null);

        Assert.IsType<IdentityCheck.Unauthorized>(await client.WhoAmIAsync(CancellationToken.None));
        Assert.IsType<PostOutcome.NotConnected>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));
        Assert.Null(exchange.Request);
    }

    [Fact]
    public async Task ARejectedTokenIsUnauthorized()
    {
        var (client, _) = ClientOver(HttpStatusCode.Unauthorized, null);

        Assert.IsType<IdentityCheck.Unauthorized>(await client.WhoAmIAsync(CancellationToken.None));
    }

    [Fact]
    public async Task APlayPostsTheFieldsTheSitePublishes()
    {
        var (client, exchange) = ClientOver(HttpStatusCode.OK, """{"recorded":1,"mix":"Rise","scoringModel":"phoenix"}""");

        var outcome = await client.PostAsync(Play, CaptureSource.SteamScreenshot, CancellationToken.None);

        Assert.Equal("https://piuscores.test/api/v2/players/me/plays", exchange.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, exchange.Request.Method);
        using var body = JsonDocument.Parse(exchange.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("rise", root.GetProperty("mix").GetString());
        Assert.Equal("watcher-f12", root.GetProperty("source").GetString());
        var play = Assert.Single(root.GetProperty("plays").EnumerateArray());
        Assert.Equal("Morrighan", play.GetProperty("songName").GetString());
        Assert.Equal("Single", play.GetProperty("chartType").GetString());
        Assert.Equal(20, play.GetProperty("level").GetInt32());
        Assert.Equal(911, play.GetProperty("perfects").GetInt32());
        Assert.Equal(59, play.GetProperty("greats").GetInt32());
        Assert.Equal(13, play.GetProperty("goods").GetInt32());
        Assert.Equal(3, play.GetProperty("bads").GetInt32());
        Assert.Equal(14, play.GetProperty("misses").GetInt32());
        Assert.Equal(170, play.GetProperty("maxCombo").GetInt32());
        Assert.Equal(945403, play.GetProperty("score").GetInt32());
        Assert.False(play.GetProperty("isBroken").GetBoolean());
        Assert.Equal("2026-09-22T20:13:28-04:00", play.GetProperty("playedAt").GetString());
        Assert.False(play.TryGetProperty("award", out _));
        Assert.False(root.TryGetProperty("recordBrokenAsBest", out _));
        var recorded = Assert.IsType<PostOutcome.Recorded>(outcome);
        Assert.Equal(1, recorded.Count);
        Assert.Equal("Rise", recorded.Mix);
    }

    [Fact]
    public async Task AProblemBecomesARefusalNamedByItsSlug()
    {
        var (client, _) = ClientOver(HttpStatusCode.BadRequest,
            """{"type":"https://piuscores.arroweclip.se/errors/judgments-do-not-reconcile","title":"The judgments do not produce the score.","status":400,"detail":"Play 0: 945403 vs 945408."}""",
            mediaType: "application/problem+json");

        var refused = Assert.IsType<PostOutcome.Refused>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));

        Assert.Equal("judgments-do-not-reconcile", refused.ProblemType);
        Assert.Equal("Play 0: 945403 vs 945408.", refused.Detail);
    }

    [Fact]
    public async Task AnUnknownSongIsItsOwnOutcome()
    {
        var (client, _) = ClientOver(HttpStatusCode.NotFound,
            """{"type":"https://piuscores.arroweclip.se/errors/not-found","title":"Not found.","status":404,"detail":"Play 0: no chart matches on Rise."}""",
            mediaType: "application/problem+json");

        var unknown = Assert.IsType<PostOutcome.SongUnknown>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));

        Assert.Equal("Play 0: no chart matches on Rise.", unknown.Detail);
    }

    [Fact]
    public async Task ARateLimitCarriesTheWait()
    {
        var (client, _) = ClientOver(HttpStatusCode.TooManyRequests, null, retryAfter: TimeSpan.FromSeconds(30));

        var limited = Assert.IsType<PostOutcome.RateLimited>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(30), limited.RetryAfter);
    }

    [Fact]
    public async Task AServerFailureIsReportedWithItsStatus()
    {
        var (client, _) = ClientOver(HttpStatusCode.InternalServerError, "<html>oops</html>", mediaType: "text/html");

        var failed = Assert.IsType<PostOutcome.Failed>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));

        Assert.Equal(500, failed.Status);
    }

    [Fact]
    public async Task ANetworkFailureIsReportedWithoutAStatus()
    {
        var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://piuscores.test/") };
        var client = new PiuScoresClient(http, new StubTokens("pst_secret"));

        var failed = Assert.IsType<PostOutcome.Failed>(await client.PostAsync(Play, CaptureSource.Replay, CancellationToken.None));

        Assert.Null(failed.Status);
        Assert.Contains("unreachable", failed.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("the site is unreachable");
        }
    }
}

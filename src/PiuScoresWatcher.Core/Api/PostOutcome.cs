namespace PiuScoresWatcher.Core.Api;

/// <summary>What PIU Scores did with a posted play. Each kind carries what a toast or a log line needs.</summary>
[ExcludeFromCodeCoverage]
public abstract record PostOutcome
{
    /// <summary>A sentence for the log and the replay report; the player's toast copy is the owner's and lives in App.</summary>
    public abstract string Describe();

    /// <summary>Recorded, on the mix the server confirmed.</summary>
    public sealed record Recorded(int Count, string Mix) : PostOutcome
    {
        public override string Describe() => $"recorded {Count} play(s) on {Mix}";
    }

    /// <summary>A 400 with a problem type — <c>judgments-do-not-reconcile</c>, <c>played-at-invalid</c>, …</summary>
    public sealed record Refused(string ProblemType, string? Detail) : PostOutcome
    {
        public override string Describe() => $"refused: {ProblemType}{(Detail is null ? "" : $" — {Detail}")}";
    }

    /// <summary>A 404: no chart on that mix matches the title, type and level.</summary>
    public sealed record SongUnknown(string? Detail) : PostOutcome
    {
        public override string Describe() => $"no chart matched{(Detail is null ? "" : $": {Detail}")}";
    }

    /// <summary>A 401: the token is gone or revoked.</summary>
    public sealed record Unauthorized : PostOutcome
    {
        public override string Describe() => "the token was not accepted";
    }

    /// <summary>A 429: too many requests; wait the given time before trying again.</summary>
    public sealed record RateLimited(TimeSpan? RetryAfter) : PostOutcome
    {
        public override string Describe() => $"rate limited{(RetryAfter is { } wait ? $", retry after {wait.TotalSeconds:0}s" : "")}";
    }

    /// <summary>Anything else: a 5xx, a network failure, a body that did not parse.</summary>
    public sealed record Failed(int? Status, string Message) : PostOutcome
    {
        public override string Describe() => $"failed{(Status is { } s ? $" ({s})" : "")}: {Message}";
    }
}

/// <summary>Who a token belongs to, from <c>GET api/v2/players/me</c>.</summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerIdentity(Guid UserId, string Username, string? GameTag);

/// <summary>What checking a token found.</summary>
[ExcludeFromCodeCoverage]
public abstract record IdentityCheck
{
    public sealed record Connected(PlayerIdentity Player) : IdentityCheck;

    public sealed record Unauthorized : IdentityCheck;

    public sealed record Failed(int? Status, string Message) : IdentityCheck;
}

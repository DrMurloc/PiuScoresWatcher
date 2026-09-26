namespace PiuScoresWatcher.Core.Api;

/// <summary>
///     What PIU Scores did with a session's close (D74, D82): closed, or why not, and whether it is worth sending again.
///     A 400 is final, and so is a 404 from a site that doesn't close sessions yet, whose own quiet window ends them; no
///     token, a refused one, a rate limit and anything else (a 5xx, no network) leave the close owed.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record CloseOutcome
{
    /// <summary>Whether the close should be sent again later; false once there is nothing more to do with it.</summary>
    public virtual bool WorthRetrying => false;

    /// <summary>A sentence for the log.</summary>
    public abstract string Describe();

    /// <summary>A 204: every open sitting on the mix is closed and its card on the way, or there was none.</summary>
    public sealed record Closed : CloseOutcome
    {
        public override string Describe() => "closed";
    }

    /// <summary>A 400 with its problem type: <c>mix-required</c>, <c>legacy-mix</c>, …</summary>
    public sealed record Refused(string ProblemType, string? Detail) : CloseOutcome
    {
        public override string Describe() => $"refused: {ProblemType}{(Detail is null ? "" : $" — {Detail}")}";
    }

    /// <summary>A 404: PIU Scores doesn't close sessions yet; its own quiet window ends this one.</summary>
    public sealed record NotOffered : CloseOutcome
    {
        public override string Describe() => "PIU Scores doesn't close sessions yet (404)";
    }

    /// <summary>No token is stored, so nothing was sent.</summary>
    public sealed record NotConnected : CloseOutcome
    {
        public override bool WorthRetrying => true;

        public override string Describe() => "no token is stored; nothing was sent";
    }

    /// <summary>A 401: the token is gone or revoked.</summary>
    public sealed record Unauthorized : CloseOutcome
    {
        public override bool WorthRetrying => true;

        public override string Describe() => "the token was not accepted";
    }

    /// <summary>A 429: too many requests.</summary>
    public sealed record RateLimited(TimeSpan? RetryAfter) : CloseOutcome
    {
        public override bool WorthRetrying => true;

        public override string Describe() => $"rate limited{(RetryAfter is { } wait ? $", retry after {wait.TotalSeconds:0}s" : "")}";
    }

    /// <summary>Anything else: a 5xx, a network failure, a request cut off.</summary>
    public sealed record Failed(int? Status, string Message) : CloseOutcome
    {
        public override bool WorthRetrying => true;

        public override string Describe() => $"failed{(Status is { } s ? $" ({s})" : "")}: {Message}";
    }
}

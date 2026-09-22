using PiuScoresWatcher.Core.Exceptions;

namespace PiuScoresWatcher.Core.Startup;

/// <summary>
///     What the process was started with. Every switch is a development seam — a player launches the
///     watcher with none of them — so an unknown switch is refused rather than ignored: a typo in a
///     dev command should fail at the door, not silently run the production configuration.
/// </summary>
/// <param name="ReplayFile">A screenshot to push through the pipeline once, without the game (<c>--replay</c>).</param>
/// <param name="DryRun">Read and report, never post (<c>--dry-run</c>).</param>
/// <param name="BaseUrl">
///     Where PIU Scores is (<c>--base-url</c>, else the <see cref="BaseUrlVariable" /> environment
///     variable); null means production.
/// </param>
public sealed record LaunchOptions(string? ReplayFile, bool DryRun, Uri? BaseUrl)
{
    /// <summary>Points the watcher at another site when no <c>--base-url</c> switch does — a local Aspire run, typically.</summary>
    public const string BaseUrlVariable = "PIUSCORESWATCHER_BASE_URL";

    public static readonly Uri ProductionBaseUrl = new("https://piuscores.arroweclip.se/");

    public static readonly LaunchOptions None = new(null, false, null);

    public Uri EffectiveBaseUrl => BaseUrl ?? ProductionBaseUrl;

    public static LaunchOptions Parse(IReadOnlyList<string> args, string? baseUrlFromEnvironment = null)
    {
        string? replayFile = null;
        var dryRun = false;
        Uri? baseUrl = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--replay":
                    replayFile = ValueAfter(args, ref i, arg);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--base-url":
                    baseUrl = ParseBaseUrl(ValueAfter(args, ref i, arg), arg);
                    break;
                default:
                    throw new InvalidLaunchOptionsException(
                        $"Unknown switch '{arg}'. Known switches: --replay <file>, --dry-run, --base-url <url>.");
            }
        }

        if (baseUrl is null && !string.IsNullOrWhiteSpace(baseUrlFromEnvironment))
            baseUrl = ParseBaseUrl(baseUrlFromEnvironment, BaseUrlVariable);

        return new LaunchOptions(replayFile, dryRun, baseUrl);
    }

    private static string ValueAfter(IReadOnlyList<string> args, ref int index, string @switch)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new InvalidLaunchOptionsException($"'{@switch}' needs a value.");
        return args[++index];
    }

    private static Uri ParseBaseUrl(string value, string source)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidLaunchOptionsException($"'{source}' must be an absolute http(s) URL; got '{value}'.");
        return uri;
    }
}

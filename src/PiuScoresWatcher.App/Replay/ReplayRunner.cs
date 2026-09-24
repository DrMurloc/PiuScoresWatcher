using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using PiuScoresWatcher.App.Api;
using PiuScoresWatcher.App.Ocr;
using PiuScoresWatcher.App.Security;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.App.Time;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Recognition;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Replay;

/// <summary>
///     <c>--replay &lt;screenshot&gt;</c>: one file through the whole pipeline with no game open,
///     reported as JSON on the console that launched us. The way the reader is developed, and the
///     way a player's failed screen is reproduced. Exit 0 is a play that reconciles, 1 a frame that
///     is not one (not a play, numbers not landed, unreadable, refused), 3 not a result screen at all.
/// </summary>
internal static partial class ReplayRunner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Run(LaunchOptions options)
    {
        var output = AttachConsole(AttachParentProcess) ? new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true } : null;
        var (report, exitCode) = ReplayAsync(options).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(report, Json);
        if (output is not null)
        {
            output.WriteLine(json);
            output.Dispose();
        }
        else
        {
            // no console to write to (double-clicked, or a shell without one): the report goes beside the logs
            AppPaths.EnsureCreated();
            File.WriteAllText(Path.Combine(AppPaths.Logs, "replay.json"), json);
        }

        return exitCode;
    }

    private static async Task<(object Report, int ExitCode)> ReplayAsync(LaunchOptions options)
    {
        var file = options.ReplayFile!;
        var image = WpfScreenDecoder.Decode(file);
        var layout = new ResultScreenDetector().Detect(image);
        if (layout is null)
            return (new { file, image.Width, image.Height, result = "not a result screen" }, 3);

        var reading = new ResultScreenReader().Read(image, layout.Value);
        var verdict = reading.Status == ReadingStatus.Complete ? PlayChecksum.Verify(reading) : null;
        var title = reading.Status is ReadingStatus.Complete or ReadingStatus.Unreadable
            ? await new WindowsOcrTitleReader(NullLogger<WindowsOcrTitleReader>.Instance).ReadAsync(image, reading.TitleRegion, CancellationToken.None)
            : null;

        // With a token in hand the replay says who it is and, unless told --dry-run, posts a play that reconciles.
        var tokens = new EnvironmentOrStoredToken(new DpapiTokenStore(NullLogger<DpapiTokenStore>.Instance));
        object? connection = null;
        object? posting = null;
        var posted = false;
        if (!string.IsNullOrWhiteSpace(tokens.Load()))
        {
            var client = PiuScoresHttp.Client(options, tokens);
            var site = options.EffectiveBaseUrl.ToString();
            connection = await client.WhoAmIAsync(CancellationToken.None) switch
            {
                IdentityCheck.Connected c => new ConnectionReport("connected", c.Player.Username, c.Player.GameTag, site),
                IdentityCheck.Unauthorized => new ConnectionReport("unauthorized", null, null, site),
                IdentityCheck.Failed f => new ConnectionReport($"failed: {f.Message}", null, null, site),
                _ => null
            };
            if (verdict is { Reconciles: true } && title is not null && !options.DryRun)
            {
                var play = ObservedPlay.From(reading, title, new SystemClock().Now);
                var outcome = await client.PostAsync(play, CaptureSource.Replay.Token(), CancellationToken.None);
                posted = outcome is PostOutcome.Recorded;
                posting = new { outcome = outcome.GetType().Name, summary = outcome.Describe() };
            }
        }

        var report = new
        {
            file,
            image.Width,
            image.Height,
            reading.Layout,
            reading.Status,
            reading.Reason,
            mix = reading.Mix.ApiName(),
            reading.ChartType,
            reading.Level,
            title,
            judgments = reading.Judgments is { } j
                ? new { j.Perfects, j.Greats, j.Goods, j.Bads, j.Misses, j.Notes }
                : null,
            reading.MaxCombo,
            reading.Score,
            reading.AccuracyDigits,
            reading.IsBroken,
            reading.LowestGlyphScore,
            checksum = verdict is null ? null : new { verdict.Reconciles, verdict.ExpectedScore, verdict.ExpectedAccuracy, verdict.Problem },
            connection,
            posted,
            posting,
            note = connection is null ? "no token: set PIUSCORESWATCHER_TOKEN or connect in settings to post" : options.DryRun ? "--dry-run: nothing posted" : null
        };
        return (report, verdict is { Reconciles: true } ? 0 : 1);
    }

    private sealed record ConnectionReport(string Status, string? Username, string? GameTag, string Site);

    private const int AttachParentProcess = -1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}

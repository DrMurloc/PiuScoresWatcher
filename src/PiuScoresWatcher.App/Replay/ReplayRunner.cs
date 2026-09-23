using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using PiuScoresWatcher.App.Ocr;
using PiuScoresWatcher.App.Storage;
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
        var (report, exitCode) = ReplayAsync(options.ReplayFile!).GetAwaiter().GetResult();
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

    private static async Task<(object Report, int ExitCode)> ReplayAsync(string file)
    {
        var image = WpfScreenDecoder.Decode(file);
        var layout = new ResultScreenDetector().Detect(image);
        if (layout is null)
            return (new { file, image.Width, image.Height, result = "not a result screen" }, 3);

        var reading = new ResultScreenReader().Read(image, layout.Value);
        var verdict = reading.Status == ReadingStatus.Complete ? PlayChecksum.Verify(reading) : null;
        var title = reading.Status is ReadingStatus.Complete or ReadingStatus.Unreadable
            ? await new WindowsOcrTitleReader(NullLogger<WindowsOcrTitleReader>.Instance).ReadAsync(image, reading.TitleRegion, CancellationToken.None)
            : null;

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
            posted = false,
            note = "posting arrives with commit 3; a replay never posts"
        };
        return (report, verdict is { Reconciles: true } ? 0 : 1);
    }

    private const int AttachParentProcess = -1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}

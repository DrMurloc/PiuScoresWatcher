using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Exceptions;
using PiuScoresWatcher.Core.Recognition;

namespace PiuScoresWatcher.Core.Api;

/// <summary>Which way the watcher saw the screen; the API's <c>source</c> names it (1–32 characters).</summary>
public enum CaptureSource
{
    Replay,
    GameWindow,
    SteamScreenshot
}

public static class CaptureSources
{
    public static string Token(this CaptureSource source)
    {
        return source switch
        {
            CaptureSource.Replay => "watcher-replay",
            CaptureSource.GameWindow => "watcher-grab",
            CaptureSource.SteamScreenshot => "watcher-f12",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Not a capture source.")
        };
    }
}

/// <summary>
///     One play as the watcher will post it: the reading that reconciled, the title the OCR read,
///     and when it happened. The award is not here on purpose — the server derives it from the
///     judgments and would refuse a wrong claim anyway.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ObservedPlay(
    RiseMix Mix,
    string SongName,
    ChartType ChartType,
    int Level,
    Judgments Judgments,
    int MaxCombo,
    int Score,
    bool IsBroken,
    DateTimeOffset PlayedAt)
{
    /// <summary>A complete reading with its title becomes a play; anything less is refused here, not by the server.</summary>
    public static ObservedPlay From(ResultScreenReading reading, string songName, DateTimeOffset playedAt)
    {
        if (reading.Status != ReadingStatus.Complete || reading.Judgments is not { } judgments
                                                      || reading.ChartType is not { } chartType || reading.Level is not { } level
                                                      || reading.MaxCombo is not { } maxCombo || reading.Score is not { } score)
            throw new IncompletePlayException("Only a complete reading can be posted.");
        if (string.IsNullOrWhiteSpace(songName))
            throw new IncompletePlayException("A play needs the song's title to be posted.");
        return new ObservedPlay(reading.Mix, songName.Trim(), chartType, level, judgments, maxCombo, score, reading.IsBroken, playedAt);
    }
}

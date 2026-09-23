namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     Reads the song title out of the region the reader points at. Text recognition is an OS
///     service, so the port lives here and the App implements it with Windows OCR; a test hands
///     the reader a stub.
/// </summary>
public interface ITitleReader
{
    /// <summary>The title as read, or null when nothing legible is there.</summary>
    Task<string?> ReadAsync(ScreenImage image, PixelRect region, CancellationToken cancellationToken);
}

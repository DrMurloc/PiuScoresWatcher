namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     Reads the song title out of the box the reader points at. Text recognition is an OS
///     service, so the port lives here and the App implements it with Windows OCR; a test hands
///     the reader a stub.
/// </summary>
public interface ITitleReader
{
    /// <summary>
    ///     What each attempt at the title in <paramref name="box" /> reads (<see cref="TitleInk.Pages" />), in order,
    ///     skipping an attempt that reads nothing or repeats one before; empty when nothing legible is there. A page of
    ///     copies gives the one word they agree on (<see cref="TitlePage.Reading" />). The caller stops as soon as a
    ///     reading names a chart, so the later attempts cost nothing when the first one lands (D55).
    /// </summary>
    IAsyncEnumerable<string> ReadAsync(ScreenImage image, TitleBox box, CancellationToken cancellationToken);
}

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>
///     Where a title is read, and how the game draws it there (D66). A result screen has one title. Warm Up's song list
///     has two — the lit row's in the list, then the panel's over the song's video — and scrolls whichever is too long
///     for its box like a ticker, cutting it off at the box's right edge.
/// </summary>
/// <param name="Name">What the log calls it: <c>title</c> on a result screen, <c>list</c> or <c>panel</c> on the song list.</param>
/// <param name="Region">The box in the frame's pixels; for a box that scrolls, its right edge is where the game cuts a title off.</param>
/// <param name="Heavy">Set in the heavy outlined type that the last attempt thins (D55); the list's row is not.</param>
/// <param name="ScrollsPast">
///     For a box that scrolls, the length in characters past which a title may not fit it: a title that runs off the
///     box's edge is the start of a longer one, and a reading that ends a title this long may be its tail (D68). Null
///     for a box whose title never scrolls.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record TitleBox(string Name, PixelRect Region, bool Heavy, int? ScrollsPast)
{
    /// <summary>A result screen's title: one box, which never scrolls.</summary>
    public static TitleBox ResultTitle(PixelRect region)
    {
        return new TitleBox("title", region, true, null);
    }
}

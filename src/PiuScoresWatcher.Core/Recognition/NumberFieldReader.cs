using System.Globalization;

namespace PiuScoresWatcher.Core.Recognition;

/// <summary>What one number field read as: the characters, with '?' for a glyph no template claimed.</summary>
internal readonly record struct FieldReading(string Text, int GlyphCount, double LowestScore)
{
    public bool IsEmpty => GlyphCount == 0;

    public bool IsClean => GlyphCount > 0 && !Text.Contains('?');

    public int? Value => IsClean && int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>The digits alone — the form accuracy is compared in, since its '%' sometimes splits into glyphs of its own.</summary>
    public string Digits => new(Text.Where(char.IsAsciiDigit).ToArray());
}

/// <summary>Reads one field of a frame: mask, segment, classify each glyph against its font's templates.</summary>
internal static class NumberFieldReader
{
    public static FieldReading Read(ScreenImage image, PixelRect rect, MaskKind ink, string family, TemplateSet templates)
    {
        var mask = Masks.Build(image, rect, ink);
        var glyphs = GlyphSegmenter.Segment(mask);
        var text = new char[glyphs.Count];
        var lowest = 1.0;
        for (var i = 0; i < glyphs.Count; i++)
        {
            var (character, score) = templates.Classify(family, GlyphBits.Of(mask, glyphs[i]), glyphs[i].Aspect);
            var accepted = character is not null && score >= TemplateSet.Acceptance;
            text[i] = accepted ? character!.Value : '?';
            lowest = Math.Min(lowest, score);
        }

        return new FieldReading(new string(text), glyphs.Count, glyphs.Count == 0 ? 0 : lowest);
    }
}

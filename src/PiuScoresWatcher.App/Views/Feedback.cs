using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PiuScoresWatcher.App.Views;

/// <summary>
///     The small text effects the windows share: a coloured status line, a Copy template with its value
///     set in bold or as a link, and a sentence with key caps in it.
/// </summary>
internal static partial class Feedback
{
    /// <summary>Shows <paramref name="text" /> in green for success, red for failure, grey while waiting.</summary>
    public static void Say(TextBlock line, string text, bool? success)
    {
        line.Text = text;
        line.Visibility = Visibility.Visible;
        line.SetResourceReference(TextElement.ForegroundProperty, success switch
        {
            true => "SystemFillColorSuccessBrush",
            false => "SystemFillColorCriticalBrush",
            null => "TextFillColorSecondaryBrush"
        });
    }

    /// <summary>Fills <paramref name="line" /> with <paramref name="template" />, its <c>{0}</c> replaced by <paramref name="value" /> in bold.</summary>
    public static void Bold(TextBlock line, string template, string value)
    {
        Fill(line, template, new Bold(new Run(value)));
    }

    /// <summary>Fills <paramref name="line" /> with <paramref name="template" />, its <c>{0}</c> a link reading <paramref name="text" />.</summary>
    public static void Link(TextBlock line, string template, string text, Action onClick)
    {
        var link = new Hyperlink(new Run(text));
        link.Click += (_, _) => onClick();
        Fill(line, template, link);
    }

    /// <summary>Fills <paramref name="line" /> with <paramref name="template" />, each <c>{KEY}</c> in it drawn as a key cap in <paramref name="keyCap" />.</summary>
    public static void Keys(TextBlock line, string template, Style keyCap)
    {
        line.Inlines.Clear();
        var from = 0;
        foreach (Match key in KeyToken().Matches(template))
        {
            if (key.Index > from)
                line.Inlines.Add(new Run(template[from..key.Index]));
            var cap = new Border { Style = keyCap, Child = new TextBlock { Text = key.Groups[1].Value, FontSize = 11, FontWeight = FontWeights.SemiBold } };
            line.Inlines.Add(new InlineUIContainer(cap) { BaselineAlignment = BaselineAlignment.Center });
            from = key.Index + key.Length;
        }

        if (from < template.Length)
            line.Inlines.Add(new Run(template[from..]));
    }

    private static void Fill(TextBlock line, string template, Inline value)
    {
        line.Inlines.Clear();
        var at = template.IndexOf("{0}", StringComparison.Ordinal);
        if (at < 0)
        {
            line.Inlines.Add(new Run(template));
            return;
        }

        if (at > 0)
            line.Inlines.Add(new Run(template[..at]));
        line.Inlines.Add(value);
        if (at + 3 < template.Length)
            line.Inlines.Add(new Run(template[(at + 3)..]));
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex KeyToken();
}

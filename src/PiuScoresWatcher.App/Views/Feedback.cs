using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PiuScoresWatcher.App.Views;

/// <summary>The small text effects the windows share: a coloured status line, and a Copy template with its value set in bold.</summary>
internal static class Feedback
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
}

using System.Globalization;
using System.Windows.Markup;
using PiuScoresWatcher.Core.Settings;
using Windows.System.UserProfile;

namespace PiuScoresWatcher.App.Localization;

/// <summary>
///     Puts the language to work (D59): the one picked in settings, or Machine Default — Windows' preferred
///     languages placed onto the eight the watcher speaks. It sets <see cref="Copy.Culture" />, which every line
///     and every number and date reads, and the UI thread's culture, which WPF's own words follow (a text box's
///     Cut and Paste). A window takes <see cref="Xml" /> as it opens.
/// </summary>
internal static class WatcherLanguage
{
    /// <summary>Windows' display language as the process started, before anything here changed the thread's.</summary>
    private static readonly string Display = CultureInfo.CurrentUICulture.Name;

    // An explicit static constructor, so Display is read before the first Apply changes the thread's culture
    // rather than whenever the runtime gets round to it.
    static WatcherLanguage()
    {
    }

    /// <summary>Speak <paramref name="chosen" />, or Windows' language when it is null.</summary>
    public static void Apply(string? chosen)
    {
        var culture = CultureInfo.GetCultureInfo(Languages.Resolve(chosen, Machine()));
        Copy.Use(culture);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>A window's xml:lang: Japanese in Japanese glyphs, and a binding's number in the language's format.</summary>
    public static XmlLanguage Xml => XmlLanguage.GetLanguage(Copy.Culture.IetfLanguageTag);

    /// <summary>Windows' preferred languages in the player's order (Settings → Time &amp; language); the display language alone if Windows will not say.</summary>
    private static IReadOnlyList<string> Machine()
    {
        try
        {
            return GlobalizationPreferences.Languages;
        }
        catch (Exception)
        {
            return [Display];
        }
    }
}

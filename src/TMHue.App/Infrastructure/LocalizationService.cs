using System.Windows;
using Application = System.Windows.Application;

namespace TMHue.App.Infrastructure;

/// <summary>Swaps the Strings.[lang].xaml ResourceDictionary at the application level, the same
/// mechanism <see cref="ThemeService"/> uses for themes. XAML consumes strings through
/// DynamicResource (so a language change re-renders open windows live); code-behind and view
/// models read them through <see cref="Get"/>.</summary>
public static class LocalizationService
{
    public const string PortugueseBrazil = "pt-BR";
    public const string EnglishUs = "en-US";

    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { PortugueseBrazil, EnglishUs };

    public static string CurrentLanguage { get; private set; } = PortugueseBrazil;

    /// <summary>Raised after the string dictionary is swapped, so surfaces that cache text
    /// (tray menu, view models) can relabel themselves.</summary>
    public static event EventHandler? LanguageChanged;

    public static void Apply(string language)
    {
        if (!SupportedLanguages.Contains(language)) language = PortugueseBrazil;
        CurrentLanguage = language;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var uri = new Uri($"/{assemblyName};component/Resources/Strings.{language}.xaml", UriKind.Relative);

        ResourceDictionary? existing = null;
        foreach (var dict in dictionaries)
        {
            if (dict.Source is not null && dict.Source.OriginalString.Contains("/Resources/Strings."))
            {
                existing = dict;
                break;
            }
        }

        var replacement = new ResourceDictionary { Source = uri };
        if (existing is not null)
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        else
            dictionaries.Add(replacement);

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Looks up a localized string by resource key; returns the key itself when
    /// missing, so a forgotten entry shows up visibly instead of crashing.</summary>
    public static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    /// <summary>Shorthand for <c>string.Format(Get(key), args)</c>.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);
}

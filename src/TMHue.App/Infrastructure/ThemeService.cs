using System.Windows;
using Microsoft.Win32;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using Application = System.Windows.Application;

namespace TMHue.App.Infrastructure;

/// <summary>Swaps the Light/Dark ResourceDictionary at the application level and tracks Windows' AppsUseLightTheme setting.</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public AppTheme CurrentPreference { get; private set; } = AppTheme.System;

    public bool IsDarkEffective { get; private set; }

    public event EventHandler? EffectiveThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(AppTheme preference)
    {
        CurrentPreference = preference;
        var isDark = preference switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDarkTheme()
        };

        if (isDark == IsDarkEffective && Application.Current.Resources.MergedDictionaries.Count > 0)
        {
            // still (re)apply on first call
        }

        IsDarkEffective = isDark;
        ApplyDictionary(isDark);
        EffectiveThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyDictionary(bool isDark)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var themeDictUri = new Uri(
            $"/{assemblyName};component/Themes/{(isDark ? "Dark" : "Light")}.xaml", UriKind.Relative);

        ResourceDictionary? existingTheme = null;
        foreach (var dict in dictionaries)
        {
            if (dict.Source is not null && dict.Source.OriginalString.Contains("/Themes/"))
            {
                existingTheme = dict;
                break;
            }
        }

        var newTheme = new ResourceDictionary { Source = themeDictUri };
        if (existingTheme is not null)
        {
            var index = dictionaries.IndexOf(existingTheme);
            dictionaries[index] = newTheme;
        }
        else
        {
            dictionaries.Add(newTheme);
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && CurrentPreference == AppTheme.System)
        {
            Application.Current.Dispatcher.Invoke(() => Apply(AppTheme.System));
        }
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}

using TMHue.Core.Models;

namespace TMHue.Core.Interfaces;

public interface IThemeService
{
    AppTheme CurrentPreference { get; }

    /// <summary>Resolved effective theme, accounting for System preference and OS setting.</summary>
    bool IsDarkEffective { get; }

    event EventHandler? EffectiveThemeChanged;

    void Apply(AppTheme preference);
}

namespace TMHue.Core.Models;

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public CopyFormat CopyFormat { get; set; } = CopyFormat.Hex;

    public bool StartWithWindows { get; set; }

    public bool CloseToTray { get; set; } = true;

    public bool ShowCaptureNotification { get; set; } = true;

    public bool ShowMagnifier { get; set; } = true;

    /// <summary>Side (in pixels) of the square region averaged into the captured/hovered color.
    /// 1 means the exact pixel under the cursor; 5 or 11 take the median of a small area, which
    /// is steadier against compression noise, anti-aliasing and gradients.</summary>
    public int SampleAreaSize { get; set; } = 1;

    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;

    public HotkeyDefinition OpenAppHotkey { get; set; } = HotkeyDefinition.DefaultOpenApp;

    /// <summary>Absent from settings.json files saved before this hotkey existed; the property
    /// initializer below supplies the default for those legacy files automatically on load.</summary>
    public HotkeyDefinition ContrastCheckerHotkey { get; set; } = HotkeyDefinition.DefaultOpenContrastChecker;

    /// <summary>Última checagem automática de atualização bem-sucedida (UTC). A checagem em
    /// segundo plano roda no máximo uma vez a cada 24 horas; a verificação manual em
    /// Configurações ignora este carimbo.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double WindowWidth { get; set; } = 440;

    public double WindowHeight { get; set; } = 300;

    public static AppSettings CreateDefault() => new();
}

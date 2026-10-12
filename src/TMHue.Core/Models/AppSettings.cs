namespace TMHue.Core.Models;

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>UI language as an IETF tag. Only "pt-BR" and "en-US" are shipped; unknown
    /// values fall back to "pt-BR" (the original language) at load time.</summary>
    public string Language { get; set; } = "pt-BR";

    public CopyFormat CopyFormat { get; set; } = CopyFormat.Hex;

    public bool StartWithWindows { get; set; }

    public bool CloseToTray { get; set; } = true;

    public bool ShowCaptureNotification { get; set; } = true;

    public bool ShowMagnifier { get; set; } = true;
    public bool UsePrecisionCursor { get; set; }
    public bool FreezeScreenWhilePicking { get; set; }
    /// <summary>Maximum channel difference ignored by Shift+arrows. Simple arrows remain 1px.</summary>
    public int ColorChangeTolerance { get; set; } = 2;
    public CaptureColorOutput ColorOutput { get; set; } = CaptureColorOutput.Rendered;

    /// <summary>1 selects the exact captured pixel. 5/11 select an actual regional pixel
    /// nearest to the channel median; existing persisted preferences are preserved.</summary>
    public int SampleAreaSize { get; set; } = 1;

    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;

    public HotkeyDefinition OpenAppHotkey { get; set; } = HotkeyDefinition.DefaultOpenApp;

    /// <summary>Absent from settings.json files saved before this hotkey existed; the property
    /// initializer below supplies the default for those legacy files automatically on load.</summary>
    public HotkeyDefinition ContrastCheckerHotkey { get; set; } = HotkeyDefinition.DefaultOpenContrastChecker;

    /// <summary>Same legacy-file rationale as <see cref="ContrastCheckerHotkey"/>.</summary>
    public HotkeyDefinition HarmonyHotkey { get; set; } = HotkeyDefinition.DefaultOpenHarmony;

    /// <summary>Same legacy-file rationale as <see cref="ContrastCheckerHotkey"/>.</summary>
    public HotkeyDefinition PaletteExtractorHotkey { get; set; } = HotkeyDefinition.DefaultOpenPaletteExtractor;

    /// <summary>Última checagem automática de atualização bem-sucedida (UTC). A checagem em
    /// segundo plano roda no máximo uma vez a cada 24 horas; a verificação manual em
    /// Configurações ignora este carimbo.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>Última verificação manual concluída pelo botão "Verificar" em Configurações
    /// (UTC). O botão respeita um intervalo mínimo de 2 horas entre verificações, para não
    /// gerar tráfego desnecessário contra a GitHub; persistido para o limite sobreviver a
    /// reinícios do app.</summary>
    public DateTime? LastManualUpdateCheckUtc { get; set; }

    /// <summary>Quando false, o app nunca consulta as GitHub Releases por conta própria; a
    /// verificação manual em Configurações continua disponível.</summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    /// <summary>Versão instalada para a qual o tamanho exibido em Configurações > Aplicativos
    /// já foi calculado; evita percorrer a pasta de instalação a cada início.</summary>
    public string? UninstallSizeVersion { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double WindowWidth { get; set; } = 440;

    public double WindowHeight { get; set; } = 300;

    public static AppSettings CreateDefault() => new();

    /// <summary>Repairs values that are valid JSON but invalid settings (a null hotkey, an
    /// out-of-range enum, a non-finite coordinate), so a hand-edited or damaged file can never
    /// take startup down. Called once, right after deserialization.</summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(Theme)) Theme = AppTheme.Dark;
        if (!Enum.IsDefined(CopyFormat)) CopyFormat = CopyFormat.Hex;
        if (!Enum.IsDefined(ColorOutput)) ColorOutput = CaptureColorOutput.Rendered;
        if (string.IsNullOrWhiteSpace(Language)) Language = "pt-BR";
        if (SampleAreaSize is not (1 or 5 or 11)) SampleAreaSize = 1;
        ColorChangeTolerance = Math.Clamp(ColorChangeTolerance, 0, 32);

        Hotkey = NormalizeHotkey(Hotkey, HotkeyDefinition.Default);
        OpenAppHotkey = NormalizeHotkey(OpenAppHotkey, HotkeyDefinition.DefaultOpenApp);
        ContrastCheckerHotkey = NormalizeHotkey(ContrastCheckerHotkey, HotkeyDefinition.DefaultOpenContrastChecker);
        HarmonyHotkey = NormalizeHotkey(HarmonyHotkey, HotkeyDefinition.DefaultOpenHarmony);
        PaletteExtractorHotkey = NormalizeHotkey(PaletteExtractorHotkey, HotkeyDefinition.DefaultOpenPaletteExtractor);

        if (WindowLeft is { } left && !double.IsFinite(left)) WindowLeft = null;
        if (WindowTop is { } top && !double.IsFinite(top)) WindowTop = null;
    }

    private static readonly string[] KnownModifiers = { "Control", "Alt", "Shift", "Windows" };

    // A hotkey without a recognized modifier would grab a plain key system-wide.
    private static HotkeyDefinition NormalizeHotkey(HotkeyDefinition? hotkey, HotkeyDefinition fallback)
    {
        if (hotkey is null) return fallback;

        List<string>? modifiers = hotkey.Modifiers;
        var known = modifiers?.Where(m => KnownModifiers.Contains(m)).Distinct().ToList();
        hotkey.Modifiers = known is { Count: > 0 } ? known : fallback.Modifiers;
        if (string.IsNullOrWhiteSpace(hotkey.Key)) hotkey.Key = fallback.Key;
        return hotkey;
    }
}

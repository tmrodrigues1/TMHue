using TMHue.App.Infrastructure;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;

namespace TMHue.App.ViewModels;

/// <summary>
/// TMHue ships with a single, fixed pure-black theme, so there is intentionally no theme
/// picker here — only the settings that are actually user-facing choices.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private const string CapturePromptText = "Pressione a combinação desejada (Esc para cancelar)...";

    private readonly AppSettings _settings;
    private readonly ISettingsRepository _repository;
    private readonly IStartupService _startupService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly UpdateService _updateService;

    private string? _hotkeyError;
    private string _updateStatus;
    private bool _checkingForUpdates;

    public SettingsViewModel(
        AppSettings settings,
        ISettingsRepository repository,
        IStartupService startupService,
        IGlobalHotkeyService hotkeyService,
        UpdateService updateService)
    {
        _settings = settings;
        _repository = repository;
        _startupService = startupService;
        _hotkeyService = hotkeyService;
        _updateService = updateService;
        _updateStatus = $"Versão atual: {_updateService.CurrentVersionDisplay}";
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        private set { _updateStatus = value; OnPropertyChanged(); }
    }

    public bool CanCheckForUpdates => !_checkingForUpdates;

    /// <summary>Verificação manual disparada pelo botão em Configurações; ignora o
    /// intervalo de 24 h da checagem automática.</summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_checkingForUpdates) return;
        _checkingForUpdates = true;
        OnPropertyChanged(nameof(CanCheckForUpdates));
        UpdateStatus = "Verificando atualizações…";

        var result = await _updateService.CheckManuallyAsync();
        UpdateStatus = result switch
        {
            UpdateCheckResult.UpToDate => $"Você já está na versão mais recente ({_updateService.CurrentVersionDisplay}).",
            UpdateCheckResult.UpdateAvailable => "Nova versão disponível — use o aviso para atualizar.",
            UpdateCheckResult.NotInstalled => "Atualizações automáticas ficam disponíveis na versão instalada pelo Setup.",
            _ => "Não foi possível verificar agora. Tente novamente mais tarde."
        };

        _checkingForUpdates = false;
        OnPropertyChanged(nameof(CanCheckForUpdates));
    }

    /// <summary>Reads/writes the actual Run-key registration rather than the cached settings
    /// flag, so the checkbox always reflects whether startup is truly active.</summary>
    public bool StartWithWindows
    {
        get => _startupService.IsEnabled;
        set
        {
            _startupService.SetEnabled(value);
            _settings.StartWithWindows = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set { _settings.CloseToTray = value; OnPropertyChanged(); Persist(); }
    }

    public bool ShowCaptureNotification
    {
        get => _settings.ShowCaptureNotification;
        set { _settings.ShowCaptureNotification = value; OnPropertyChanged(); Persist(); }
    }

    public bool ShowMagnifier
    {
        get => _settings.ShowMagnifier;
        set { _settings.ShowMagnifier = value; OnPropertyChanged(); Persist(); }
    }

    /// <summary>Sample area sizes backing <see cref="SampleAreaOptions"/>, in display order.</summary>
    private static readonly int[] SampleAreaSizes = { 1, 5, 11 };

    /// <summary>Dropdown labels for the sample-area ComboBox, in the same order as <see cref="SampleAreaSizes"/>.</summary>
    public IReadOnlyList<string> SampleAreaOptions { get; } = new[]
    {
        "Pixel exato",
        "Média 5×5 (recomendado)",
        "Média 11×11"
    };

    public int SelectedSampleAreaIndex
    {
        get => Math.Max(0, Array.IndexOf(SampleAreaSizes, _settings.SampleAreaSize));
        set
        {
            if (value < 0 || value >= SampleAreaSizes.Length) return;
            _settings.SampleAreaSize = SampleAreaSizes[value];
            OnPropertyChanged();
            Persist();
        }
    }

    /// <summary>Copy formats backing <see cref="CopyFormatOptions"/>, in display order.</summary>
    private static readonly CopyFormat[] CopyFormats = { CopyFormat.Hex, CopyFormat.Rgb, CopyFormat.Hsl };

    /// <summary>Dropdown labels for the copy-format ComboBox, in the same order as <see cref="CopyFormats"/>.
    /// Default format used when copying a captured color; can be overridden per-capture with the
    /// 1/2/3 shortcuts while the eyedropper is active, without changing this default.</summary>
    public IReadOnlyList<string> CopyFormatOptions { get; } = new[]
    {
        "HEX (#RRGGBB)",
        "RGB (rgb(r, g, b))",
        "HSL (hsl(h, s%, l%))"
    };

    public int SelectedCopyFormatIndex
    {
        get => Math.Max(0, Array.IndexOf(CopyFormats, _settings.CopyFormat));
        set
        {
            if (value < 0 || value >= CopyFormats.Length) return;
            _settings.CopyFormat = CopyFormats[value];
            OnPropertyChanged();
            Persist();
        }
    }

    /// <summary>Id of the hotkey currently being captured (<see cref="HotkeyIds"/>), or null
    /// when no capture is in progress.</summary>
    public string? CapturingHotkeyId { get; private set; }

    public bool IsCapturingAnyHotkey => CapturingHotkeyId is not null;

    public string? HotkeyError
    {
        get => _hotkeyError;
        private set { _hotkeyError = value; OnPropertyChanged(); }
    }

    public string CaptureHotkeyDisplay => CapturingHotkeyId == HotkeyIds.Capture
        ? CapturePromptText
        : _settings.Hotkey.ToString();

    public string OpenAppHotkeyDisplay => CapturingHotkeyId == HotkeyIds.OpenApp
        ? CapturePromptText
        : _settings.OpenAppHotkey.ToString();

    public string ContrastCheckerHotkeyDisplay => CapturingHotkeyId == HotkeyIds.OpenContrastChecker
        ? CapturePromptText
        : _settings.ContrastCheckerHotkey.ToString();

    public void BeginCaptureHotkey(string hotkeyId)
    {
        HotkeyError = null;
        CapturingHotkeyId = hotkeyId;
        RaiseHotkeyDisplaysChanged();
    }

    public void CancelCaptureHotkey()
    {
        CapturingHotkeyId = null;
        RaiseHotkeyDisplaysChanged();
    }

    /// <summary>Attempts to switch the given hotkey to the given combination. Restores the
    /// previous hotkey and reports an error when the new combination is already taken by
    /// another application.</summary>
    public bool TrySetHotkey(string hotkeyId, List<string> modifiers, string key)
    {
        var candidate = new HotkeyDefinition { Modifiers = modifiers, Key = key };
        var previous = hotkeyId switch
        {
            HotkeyIds.Capture => _settings.Hotkey,
            HotkeyIds.OpenApp => _settings.OpenAppHotkey,
            _ => _settings.ContrastCheckerHotkey
        };

        _hotkeyService.Unregister(hotkeyId);
        if (!_hotkeyService.TryRegister(hotkeyId, candidate))
        {
            _hotkeyService.TryRegister(hotkeyId, previous);
            HotkeyError = "Essa combinação já está em uso por outro aplicativo.";
            CapturingHotkeyId = null;
            RaiseHotkeyDisplaysChanged();
            return false;
        }

        switch (hotkeyId)
        {
            case HotkeyIds.Capture:
                _settings.Hotkey = candidate;
                break;
            case HotkeyIds.OpenApp:
                _settings.OpenAppHotkey = candidate;
                break;
            default:
                _settings.ContrastCheckerHotkey = candidate;
                break;
        }

        HotkeyError = null;
        CapturingHotkeyId = null;
        RaiseHotkeyDisplaysChanged();
        Persist();
        return true;
    }

    private void RaiseHotkeyDisplaysChanged()
    {
        OnPropertyChanged(nameof(IsCapturingAnyHotkey));
        OnPropertyChanged(nameof(CaptureHotkeyDisplay));
        OnPropertyChanged(nameof(OpenAppHotkeyDisplay));
        OnPropertyChanged(nameof(ContrastCheckerHotkeyDisplay));
    }

    private void Persist() => _repository.Save(_settings);
}

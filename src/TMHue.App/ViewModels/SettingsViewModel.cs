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
    // Short enough to fit the hotkeys card's single-line rows without pushing the button.
    private const string CapturePromptText = "Pressione a combinação…";

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
        _updateStatus = _updateService.ManualCheckCooldownRemaining is { } remaining
            ? $"Versão atual: {_updateService.CurrentVersionDisplay} · {FormatCooldown(remaining)}"
            : $"Versão atual: {_updateService.CurrentVersionDisplay}";
    }

    /// <summary>Mensagem curta de espera do botão "Verificar" (limite de uma verificação
    /// manual a cada 2 horas). Arredonda para cima para nunca prometer menos tempo que o real.</summary>
    private static string FormatCooldown(TimeSpan remaining)
    {
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        var text = minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
        return $"Nova verificação em {text}.";
    }

    /// <summary>Exposed so the window can hand the service to the update-progress modal after
    /// the user consents.</summary>
    public UpdateService UpdateService => _updateService;

    public string UpdateStatus
    {
        get => _updateStatus;
        private set { _updateStatus = value; OnPropertyChanged(); }
    }

    // Desabilita o botão durante a checagem e durante o intervalo mínimo de 2 h entre
    // verificações manuais (o tempo restante aparece no texto de status ao lado).
    public bool CanCheckForUpdates => !_checkingForUpdates && _updateService.ManualCheckCooldownRemaining is null;

    private string? _availableVersion;

    /// <summary>Versão aguardando o consentimento do usuário, ou null quando não há atualização
    /// pendente. A atualização nunca começa sozinha: só após o clique em "Atualizar".</summary>
    public string? AvailableVersion
    {
        get => _availableVersion;
        private set
        {
            _availableVersion = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUpdateAvailable));
        }
    }

    public bool IsUpdateAvailable => AvailableVersion is not null;

    /// <summary>Adia a atualização pendente. O pacote continua conhecido pelo UpdateService;
    /// uma nova checagem (ou o próximo ciclo automático) oferece a mesma versão de novo.</summary>
    public void DeclineUpdate()
    {
        AvailableVersion = null;
        UpdateStatus = "Atualização adiada.";
    }

    /// <summary>Chamado pela janela quando o download/aplicação falha (em sucesso o app
    /// reinicia e nunca chegamos aqui). Mantém a oferta visível para tentar de novo.</summary>
    public void ReportUpdateFailed() => UpdateStatus = "Falha ao atualizar. Tente novamente.";

    /// <summary>Verificação manual disparada pelo botão em Configurações; ignora o
    /// intervalo de 24 h da checagem automática. Mensagens curtas: o card tem uma linha.</summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_checkingForUpdates) return;
        _checkingForUpdates = true;
        OnPropertyChanged(nameof(CanCheckForUpdates));
        UpdateStatus = "Verificando…";

        var result = await _updateService.CheckManuallyAsync();
        if (result == UpdateCheckResult.UpdateAvailable)
        {
            AvailableVersion = _updateService.PendingVersion;
            UpdateStatus = $"Nova versão disponível: {AvailableVersion}";
        }
        else
        {
            AvailableVersion = null;
            UpdateStatus = result switch
            {
                UpdateCheckResult.UpToDate when _updateService.ManualCheckCooldownRemaining is { } remaining =>
                    $"Você está atualizado. {FormatCooldown(remaining)}",
                UpdateCheckResult.UpToDate => "Você está atualizado.",
                UpdateCheckResult.Throttled when _updateService.ManualCheckCooldownRemaining is { } remaining =>
                    FormatCooldown(remaining),
                UpdateCheckResult.Throttled => "Aguarde para verificar novamente.",
                UpdateCheckResult.NotInstalled => "Disponível apenas na versão instalada.",
                _ => "Não foi possível verificar."
            };
        }

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

    /// <summary>Dropdown labels for the sample-area ComboBox, in the same order as <see cref="SampleAreaSizes"/>.
    /// Short on purpose: the combo now shares a card row with the copy-format combo (~150px each),
    /// so anything longer gets clipped; the "recomendado" guidance lives in the card's tooltip.</summary>
    public IReadOnlyList<string> SampleAreaOptions { get; } = new[]
    {
        "Pixel exato",
        "Média 5×5",
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
        "HEX",
        "RGB",
        "HSL"
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

    public string HarmonyHotkeyDisplay => CapturingHotkeyId == HotkeyIds.OpenHarmony
        ? CapturePromptText
        : _settings.HarmonyHotkey.ToString();

    public string PaletteExtractorHotkeyDisplay => CapturingHotkeyId == HotkeyIds.OpenPaletteExtractor
        ? CapturePromptText
        : _settings.PaletteExtractorHotkey.ToString();

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
            HotkeyIds.OpenHarmony => _settings.HarmonyHotkey,
            HotkeyIds.OpenPaletteExtractor => _settings.PaletteExtractorHotkey,
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
            case HotkeyIds.OpenHarmony:
                _settings.HarmonyHotkey = candidate;
                break;
            case HotkeyIds.OpenPaletteExtractor:
                _settings.PaletteExtractorHotkey = candidate;
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
        OnPropertyChanged(nameof(HarmonyHotkeyDisplay));
        OnPropertyChanged(nameof(PaletteExtractorHotkeyDisplay));
    }

    private void Persist() => _repository.Save(_settings);
}

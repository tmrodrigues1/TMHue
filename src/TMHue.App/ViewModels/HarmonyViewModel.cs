using System.Collections.ObjectModel;
using TMHue.App.Infrastructure;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TMHue.App.ViewModels;

/// <summary>One clickable color chip inside a harmony group: frozen brush for the swatch,
/// formatted value (in the user's default format) for the label and for what gets copied.</summary>
public sealed class HarmonySwatch
{
    public HarmonySwatch(RgbColor color, CopyFormat format)
    {
        Color = color;
        Value = color.Format(format);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        Brush = brush;
    }

    public RgbColor Color { get; }
    public string Value { get; }
    public System.Windows.Media.Brush Brush { get; }

    /// <summary>Localized "Clique para copiar {value}" tooltip. Computed at construction —
    /// swatches are rebuilt whenever the palette changes, which is often enough.</summary>
    public string CopyTooltip => LocalizationService.Format("L.Common.ClickToCopyFmt", Value);
}

/// <summary>A titled row of harmony swatches (e.g. "Complementar" with its two colors), plus a
/// short plain-language explanation shown in the section's help tooltip.</summary>
public sealed record HarmonyGroup(string Title, string Description, IReadOnlyList<HarmonySwatch> Swatches);

/// <summary>Backs the harmony generator window: a base color (typed or eyedropper-picked, with
/// the mini zoom the coordinator already provides) and the derived harmony groups —
/// complementar, análoga, triádica, tetrádica and tint/shade scales. Clicking any swatch copies
/// it in the user's default format.</summary>
public sealed class HarmonyViewModel : ViewModelBase, IDisposable
{
    private static string InvalidColorMessage => LocalizationService.Get("L.Contrast.InvalidColor");

    private readonly ColorPickerCoordinator _coordinator;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;
    private readonly Func<AppSettings> _settingsAccessor;

    // Same teardown contract as ContrastCheckerViewModel: a capture this window started must be
    // unsubscribed/cancelled if the window closes before the pick finishes.
    private EventHandler<CapturedColor>? _pendingCaptureHandler;

    private RgbColor _baseColor = new(0x7C, 0x4D, 0xFF);
    private string _baseHex = string.Empty;

    /// <summary>Exposed so the window can avoid closing itself on Esc mid-capture.</summary>
    public ColorPickerCoordinator Coordinator => _coordinator;

    // The format every value in this modal is rendered in. Starts as the user's configured
    // default, and follows the 1/2/3 format keys pressed during an eyedropper capture, so the
    // base color and every harmony swatch always match what the picker is showing.
    private CopyFormat _displayFormat;

    public HarmonyViewModel(
        ColorPickerCoordinator coordinator,
        IClipboardService clipboard,
        INotificationService notifications,
        Func<AppSettings> settingsAccessor)
    {
        _coordinator = coordinator;
        _clipboard = clipboard;
        _notifications = notifications;
        _settingsAccessor = settingsAccessor;
        _displayFormat = settingsAccessor().CopyFormat;

        PickBaseColorCommand = new RelayCommand(StartPick);
        CopySwatchCommand = new RelayCommand<HarmonySwatch>(CopySwatch);

        _coordinator.ActiveFormatChanged += OnActiveFormatChanged;

        _baseHex = _baseColor.Format(_displayFormat);
        Rebuild();
    }

    // _settingsAccessor stays: the format keys only apply while this window lives; a freshly
    // opened harmony window always starts back at the configured default.

    /// <summary>Pressing 1/2/3 while the eyedropper is active re-renders this whole modal in
    /// the chosen format immediately — base color input and every harmony swatch.</summary>
    private void OnActiveFormatChanged(object? sender, CopyFormat format)
    {
        if (_displayFormat == format) return;
        _displayFormat = format;

        // Reformat the input without re-parsing side effects: _baseColor is already the parsed
        // value, so just re-render it and rebuild the groups.
        SetField(ref _baseHex, _baseColor.Format(format), nameof(BaseHex));
        ErrorMessage = null;
        Rebuild();
    }

    public RelayCommand PickBaseColorCommand { get; }

    public RelayCommand<HarmonySwatch> CopySwatchCommand { get; }

    public ObservableCollection<HarmonyGroup> Groups { get; } = new();

    public string BaseHex
    {
        get => _baseHex;
        set
        {
            if (!SetField(ref _baseHex, value)) return;

            if (RgbColor.TryParse(value, out var parsed))
            {
                ErrorMessage = null;
                _baseColor = parsed;
                Rebuild();
            }
            else
            {
                ErrorMessage = InvalidColorMessage;
            }
        }
    }

    private System.Windows.Media.Brush _baseBrush = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush BaseBrush { get => _baseBrush; private set => SetField(ref _baseBrush, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    // Same confirmation surface as a screen capture: the centered Windows toast, so copying a
    // harmony color feels identical to copying a captured one.
    private void CopySwatch(HarmonySwatch? swatch)
    {
        if (swatch is null) return;
        if (_clipboard.TrySetText(swatch.Value))
            _notifications.ShowCaptureConfirmation(swatch.Color.ToHex(), swatch.Value);
    }

    private void StartPick()
    {
        if (_coordinator.State != PickerState.Idle) return;

        EventHandler<CapturedColor>? onCaptured = null;
        onCaptured = (_, captured) =>
        {
            _coordinator.Captured -= onCaptured;
            _pendingCaptureHandler = null;
            // _displayFormat already tracked any 1/2/3 presses during this capture.
            BaseHex = captured.ToRgb().Format(_displayFormat);
        };
        _pendingCaptureHandler = onCaptured;
        _coordinator.Captured += onCaptured;
        _coordinator.BeginCapture();
    }

    public void Dispose()
    {
        _coordinator.ActiveFormatChanged -= OnActiveFormatChanged;

        if (_pendingCaptureHandler is not { } handler) return;

        _coordinator.Captured -= handler;
        _pendingCaptureHandler = null;
        _coordinator.CancelCapture();
    }

    private void Rebuild()
    {
        var brush = new SolidColorBrush(Color.FromRgb(_baseColor.Red, _baseColor.Green, _baseColor.Blue));
        brush.Freeze();
        BaseBrush = brush;

        var format = _displayFormat;

        Groups.Clear();
        Groups.Add(BuildGroup("L.Harmony.Complementary", "L.Harmony.ComplementaryDesc",
            ColorHarmonies.Complementary(_baseColor), format));
        Groups.Add(BuildGroup("L.Harmony.Analogous", "L.Harmony.AnalogousDesc",
            ColorHarmonies.Analogous(_baseColor), format));
        Groups.Add(BuildGroup("L.Harmony.Triadic", "L.Harmony.TriadicDesc",
            ColorHarmonies.Triadic(_baseColor), format));
        Groups.Add(BuildGroup("L.Harmony.Tetradic", "L.Harmony.TetradicDesc",
            ColorHarmonies.Tetradic(_baseColor), format));
        Groups.Add(BuildGroup("L.Harmony.Tints", "L.Harmony.TintsDesc",
            ColorHarmonies.Tints(_baseColor), format));
        Groups.Add(BuildGroup("L.Harmony.Shades", "L.Harmony.ShadesDesc",
            ColorHarmonies.Shades(_baseColor), format));
    }

    private static HarmonyGroup BuildGroup(string titleKey, string descriptionKey, RgbColor[] colors, CopyFormat format) =>
        new(LocalizationService.Get(titleKey), LocalizationService.Get(descriptionKey),
            colors.Select(c => new HarmonySwatch(c, format)).ToArray());
}

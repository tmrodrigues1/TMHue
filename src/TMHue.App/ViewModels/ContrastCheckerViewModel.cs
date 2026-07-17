using System.Text.RegularExpressions;
using TMHue.App.Infrastructure;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TMHue.App.ViewModels;

/// <summary>Backs the WCAG contrast checker window: two user-entered hex colors (text and
/// background), the resulting contrast ratio, pass/fail against every standard WCAG level, and
/// a bonus suggestion for the smallest change that would get a failing pair to pass AA.</summary>
public sealed class ContrastCheckerViewModel : ViewModelBase, IDisposable
{
    private static string InvalidColorMessage => Infrastructure.LocalizationService.Get("L.Contrast.InvalidColor");

    private readonly ColorPickerCoordinator _coordinator;
    private readonly Func<AppSettings> _settingsAccessor;

    private static readonly RgbColor DefaultTextColor = new(0x00, 0x00, 0x00);
    private static readonly RgbColor DefaultBackgroundColor = new(0xFF, 0xFF, 0xFF);

    private string _textHex = "#000000";
    private string _backgroundHex = "#FFFFFF";

    // Tracks a capture this view model itself started, so it can be torn down cleanly (both the
    // event subscription and, if still in flight, the capture itself) when the window closes
    // before the user finishes or cancels the pick.
    private EventHandler<CapturedColor>? _pendingCaptureHandler;

    /// <summary>Exposed so the window can check <see cref="ColorPickerCoordinator.State"/> and
    /// avoid closing itself on Esc while an eyedropper capture it started is still in progress.</summary>
    public ColorPickerCoordinator Coordinator => _coordinator;

    public ContrastCheckerViewModel(ColorPickerCoordinator coordinator, Func<AppSettings> settingsAccessor)
    {
        _coordinator = coordinator;
        _settingsAccessor = settingsAccessor;
        PickTextColorCommand = new RelayCommand(() => StartPick(color => TextHex = color));
        PickBackgroundColorCommand = new RelayCommand(() => StartPick(color => BackgroundHex = color));

        var format = _settingsAccessor().CopyFormat;
        _textHex = DefaultTextColor.Format(format);
        _backgroundHex = DefaultBackgroundColor.Format(format);
        Recalculate();
    }

    /// <summary>Discreet label showing the color format the user has set as default in
    /// Configurações, so the inputs above make sense without opening the tooltip.</summary>
    public string DefaultFormatLabel =>
        Infrastructure.LocalizationService.Format("L.Contrast.DefaultFormatFmt", _settingsAccessor().CopyFormat.ToLabel());

    /// <summary>Longest string <see cref="RgbColor.Format"/> can ever produce for each format —
    /// "#RRGGBB" (7), "rgb(255, 255, 255)" (19), "hsl(359, 100%, 100%)" (21) — used as the input
    /// boxes' hard cap so neither format can be typed or pasted past its own valid length.</summary>
    private static int MaxLengthFor(CopyFormat format) => format switch
    {
        CopyFormat.Rgb => 19,
        CopyFormat.Hsl => 21,
        _ => 7
    };

    public int MaxColorInputLength => MaxLengthFor(_settingsAccessor().CopyFormat);

    // Exactly the characters that can ever appear in each format's own output — nothing else is
    // a valid part of that color, so nothing else is typeable or pasteable. Lowercase-only for
    // the RGB/HSL function names since Format() never produces uppercase ones; HEX allows both
    // cases since ToHex() defaults to uppercase but lowercase is equally valid input.
    private static readonly Regex HexAllowedCharacters = new(@"^[#0-9a-fA-F]+$", RegexOptions.Compiled);
    private static readonly Regex RgbAllowedCharacters = new(@"^[0-9,() rgb]+$", RegexOptions.Compiled);
    private static readonly Regex HslAllowedCharacters = new(@"^[0-9,()% hsl.\-]+$", RegexOptions.Compiled);

    /// <summary>The character whitelist matching the currently selected default format, so the
    /// window's input filtering can never let through a character that format's own <see
    /// cref="RgbColor.Format"/> output would never contain (a first line of defense against
    /// malformed or malicious input, on top of <see cref="RgbColor.TryParse"/>'s own validation).</summary>
    public Regex AllowedColorCharacters => _settingsAccessor().CopyFormat switch
    {
        CopyFormat.Rgb => RgbAllowedCharacters,
        CopyFormat.Hsl => HslAllowedCharacters,
        _ => HexAllowedCharacters
    };

    /// <summary>Re-reads the default format after Configurações closes, in case it changed, and
    /// reformats whichever inputs currently hold a validly-parsed color so they match it — e.g.
    /// switching the default to RGB turns "#FFFFFF" into "rgb(255, 255, 255)".</summary>
    public void RefreshDefaultFormat()
    {
        OnPropertyChanged(nameof(DefaultFormatLabel));
        OnPropertyChanged(nameof(MaxColorInputLength));
        OnPropertyChanged(nameof(AllowedColorCharacters));

        var format = _settingsAccessor().CopyFormat;
        if (RgbColor.TryParse(TextHex, out var text))
            TextHex = text.Format(format);
        if (RgbColor.TryParse(BackgroundHex, out var background))
            BackgroundHex = background.Format(format);
    }

    public RelayCommand PickTextColorCommand { get; }

    public RelayCommand PickBackgroundColorCommand { get; }

    /// <summary>Starts an eyedropper capture via the shared coordinator and routes the result into
    /// whichever hex property the caller wants updated. The handler unsubscribes itself the moment
    /// it fires, so repeated clicks never stack up multiple listeners on the singleton coordinator.
    /// If the capture is instead cancelled (Esc) or the window is closed mid-capture, <see
    /// cref="Dispose"/> unsubscribes it — otherwise it would linger forever on the coordinator's
    /// static-lifetime <see cref="ColorPickerCoordinator.Captured"/> event.</summary>
    private void StartPick(Action<string> applyColor)
    {
        if (_coordinator.State != PickerState.Idle) return;

        EventHandler<CapturedColor>? onCaptured = null;
        onCaptured = (_, captured) =>
        {
            _coordinator.Captured -= onCaptured;
            _pendingCaptureHandler = null;
            applyColor(captured.ToRgb().Format(_settingsAccessor().CopyFormat));
        };
        _pendingCaptureHandler = onCaptured;
        _coordinator.Captured += onCaptured;
        _coordinator.BeginCapture();
    }

    /// <summary>Called when the owning window closes. Unsubscribes a still-pending capture handler
    /// (the user cancelled with Esc, or closed the window mid-capture instead of finishing the
    /// pick) and cancels the in-flight capture itself, so neither a dead handler nor an orphaned
    /// eyedropper overlay survive this view model.</summary>
    public void Dispose()
    {
        if (_pendingCaptureHandler is not { } handler) return;

        _coordinator.Captured -= handler;
        _pendingCaptureHandler = null;
        _coordinator.CancelCapture();
    }

    public string TextHex
    {
        get => _textHex;
        set
        {
            // Clearing the box entirely (e.g. select-all + delete) has no valid color to fall
            // back on, so restore the default text color in whatever format is currently active
            // instead of leaving the field empty/invalid.
            var resolved = string.IsNullOrWhiteSpace(value)
                ? DefaultTextColor.Format(_settingsAccessor().CopyFormat)
                : value;
            if (SetField(ref _textHex, resolved))
                Recalculate();
        }
    }

    public string BackgroundHex
    {
        get => _backgroundHex;
        set
        {
            var resolved = string.IsNullOrWhiteSpace(value)
                ? DefaultBackgroundColor.Format(_settingsAccessor().CopyFormat)
                : value;
            if (SetField(ref _backgroundHex, resolved))
                Recalculate();
        }
    }

    private System.Windows.Media.Brush _textBrush = System.Windows.Media.Brushes.Black;
    public System.Windows.Media.Brush TextBrush { get => _textBrush; private set => SetField(ref _textBrush, value); }

    private System.Windows.Media.Brush _backgroundBrush = System.Windows.Media.Brushes.White;
    public System.Windows.Media.Brush BackgroundBrush { get => _backgroundBrush; private set => SetField(ref _backgroundBrush, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    private string _ratioText = "—";
    public string RatioText { get => _ratioText; private set => SetField(ref _ratioText, value); }

    private bool _passesAaNormalText;
    public bool PassesAaNormalText { get => _passesAaNormalText; private set => SetField(ref _passesAaNormalText, value); }

    private bool _passesAaaNormalText;
    public bool PassesAaaNormalText { get => _passesAaaNormalText; private set => SetField(ref _passesAaaNormalText, value); }

    private bool _passesAaLargeText;
    public bool PassesAaLargeText { get => _passesAaLargeText; private set => SetField(ref _passesAaLargeText, value); }

    private bool _passesAaaLargeText;
    public bool PassesAaaLargeText { get => _passesAaaLargeText; private set => SetField(ref _passesAaaLargeText, value); }

    private string? _suggestionText;
    public string? SuggestionText { get => _suggestionText; private set => SetField(ref _suggestionText, value); }

    private void Recalculate()
    {
        if (!RgbColor.TryParse(TextHex, out var text) || !RgbColor.TryParse(BackgroundHex, out var background))
        {
            ErrorMessage = InvalidColorMessage;
            RatioText = "—";
            PassesAaNormalText = PassesAaaNormalText = PassesAaLargeText = PassesAaaLargeText = false;
            SuggestionText = null;
            return;
        }

        ErrorMessage = null;

        var textBrush = new SolidColorBrush(Color.FromRgb(text.Red, text.Green, text.Blue));
        textBrush.Freeze();
        TextBrush = textBrush;

        var backgroundBrush = new SolidColorBrush(Color.FromRgb(background.Red, background.Green, background.Blue));
        backgroundBrush.Freeze();
        BackgroundBrush = backgroundBrush;

        var evaluation = WcagContrast.Evaluate(text, background);
        RatioText = $"{evaluation.Ratio:0.00}:1";
        PassesAaNormalText = evaluation.PassesAaNormalText;
        PassesAaaNormalText = evaluation.PassesAaaNormalText;
        PassesAaLargeText = evaluation.PassesAaLargeText;
        PassesAaaLargeText = evaluation.PassesAaaLargeText;

        SuggestionText = BuildSuggestion(text, background);
    }

    private static string? BuildSuggestion(RgbColor text, RgbColor background)
    {
        var suggestion = WcagContrast.SuggestForegroundAdjustment(text, background, WcagLevel.AaNormalText);
        if (suggestion is null) return null;

        var action = Infrastructure.LocalizationService.Get(
            suggestion.Value.Darken ? "L.Contrast.Darken" : "L.Contrast.Lighten");
        return Infrastructure.LocalizationService.Format(
            "L.Contrast.SuggestionFmt", action, suggestion.Value.PercentChange);
    }
}

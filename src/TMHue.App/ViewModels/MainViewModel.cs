using System.Collections.ObjectModel;
using TMHue.App.Infrastructure;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TMHue.App.ViewModels;

/// <summary>One format's rendering of the current color (e.g. "RGB" / "rgb(47, 128, 237)").
/// <see cref="IsDefault"/> marks the user's configured default format, shown larger/prominent
/// while the other three are rendered small underneath.</summary>
public sealed record ColorFormatEntry(CopyFormat Format, string Label, string Value, bool IsDefault);

public sealed class MainViewModel : ViewModelBase
{
    private readonly ColorPickerCoordinator _coordinator;
    private readonly IColorHistoryService _history;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;
    private readonly Func<AppSettings> _settingsAccessor;

    private static readonly CopyFormat[] AllFormats = { CopyFormat.Hex, CopyFormat.Rgb, CopyFormat.Hsl };

    private RgbColor _currentColor = new(0x2F, 0x80, 0xED);
    private ColorFormatEntry _primaryFormat = new(CopyFormat.Hex, "HEX", "#2F80ED", true);
    private System.Windows.Media.Brush _currentBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x80, 0xED));

    // Set while a capture is in progress and the user presses 1/2/3 to switch the format for that
    // capture only (ColorPickerCoordinator.ActiveFormatChanged); makes the main window's own
    // readout follow the shortcut immediately instead of waiting for the next hover. Cleared when
    // a new capture starts or the current one finishes, so the persisted default format takes
    // back over.
    private CopyFormat? _captureFormatOverride;

    public MainViewModel(
        ColorPickerCoordinator coordinator,
        IColorHistoryService history,
        IClipboardService clipboard,
        INotificationService notifications,
        Func<AppSettings> settingsAccessor)
    {
        _coordinator = coordinator;
        _history = history;
        _clipboard = clipboard;
        _notifications = notifications;
        _settingsAccessor = settingsAccessor;

        _coordinator.Captured += OnCaptured;
        _coordinator.HoverColorChanged += OnHoverColorChanged;
        _coordinator.ActiveFormatChanged += OnActiveFormatChanged;
        _history.Changed += (_, _) => RefreshHistory();

        CaptureCommand = new RelayCommand(() =>
        {
            _captureFormatOverride = null;
            _coordinator.BeginCapture();
        });
        CopyCurrentCommand = new RelayCommand(() => CopyWithToast(PrimaryFormat.Value));
        CopyFormatCommand = new RelayCommand<ColorFormatEntry>(entry =>
        {
            if (entry is not null) CopyWithToast(entry.Value);
        });
        CopySwatchCommand = new RelayCommand<CapturedColor>(color =>
        {
            if (color is not null) CopyWithToast(color.Hex);
        });
        TogglePinCommand = new RelayCommand<CapturedColor>(color =>
        {
            if (color is not null) _history.TogglePin(color);
        });
        ToggleMorePanelCommand = new RelayCommand(() => IsMorePanelOpen = !IsMorePanelOpen);
        CloseMorePanelCommand = new RelayCommand(() => IsMorePanelOpen = false);
        ClearHistoryCommand = new RelayCommand(() => _history.Clear());

        SetCurrent(_currentColor);
        RefreshHistory();
    }

    /// <summary>How many swatches the main window shows; older entries go to the "Ver mais" sidebar.</summary>
    public const int MainSwatchCount = 5;

    public RelayCommand CaptureCommand { get; }
    public RelayCommand CopyCurrentCommand { get; }
    public RelayCommand<ColorFormatEntry> CopyFormatCommand { get; }
    public RelayCommand<CapturedColor> CopySwatchCommand { get; }
    public RelayCommand<CapturedColor> TogglePinCommand { get; }
    public RelayCommand ToggleMorePanelCommand { get; }
    public RelayCommand CloseMorePanelCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }

    public ObservableCollection<CapturedColor> History { get; } = new();

    public ObservableCollection<CapturedColor> MoreColors { get; } = new();

    private bool _isMorePanelOpen;
    public bool IsMorePanelOpen
    {
        get => _isMorePanelOpen;
        set
        {
            if (SetField(ref _isMorePanelOpen, value))
                OnPropertyChanged(nameof(MorePanelToggleLabel));
        }
    }

    public string MorePanelToggleLabel => IsMorePanelOpen ? "Ver menos" : "Ver mais >";

    private bool _hasMoreColors;
    public bool HasMoreColors
    {
        get => _hasMoreColors;
        private set => SetField(ref _hasMoreColors, value);
    }

    /// <summary>The raw color currently displayed, backing <see cref="PrimaryFormat"/> and
    /// <see cref="SecondaryFormats"/>. Exposed so the default format can be re-applied to it after
    /// a settings change without waiting for the next capture/hover.</summary>
    public RgbColor CurrentColor => _currentColor;

    /// <summary>The user's configured default format, rendered large/prominent.</summary>
    public ColorFormatEntry PrimaryFormat
    {
        get => _primaryFormat;
        private set => SetField(ref _primaryFormat, value);
    }

    /// <summary>The three non-default formats, rendered small underneath, one per line.</summary>
    public ObservableCollection<ColorFormatEntry> SecondaryFormats { get; } = new();

    public System.Windows.Media.Brush CurrentBrush
    {
        get => _currentBrush;
        private set => SetField(ref _currentBrush, value);
    }

    public string HotkeyDisplay => _settingsAccessor().Hotkey.ToString();
    public string OpenAppHotkeyDisplay => _settingsAccessor().OpenAppHotkey.ToString();
    public string ContrastCheckerHotkeyDisplay => _settingsAccessor().ContrastCheckerHotkey.ToString();

    /// <summary>Reads the real build version from the assembly (driven by &lt;Version&gt; in
    /// TMHue.App.csproj) instead of a hardcoded label, so the footer never drifts out of sync
    /// with an actual release. Cached: the binding re-reads it on window re-shows, and the
    /// assembly reflection behind it isn't free.</summary>
    public string VersionDisplay { get; } =
        $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    private void OnHoverColorChanged(object? sender, RgbColor color)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Mirroring the hovered color rebuilds four format readouts and a brush on every
            // mouse move — pure waste while the main window is hidden in the tray during a
            // hotkey-triggered capture. The captured color still lands via OnCaptured.
            var window = System.Windows.Application.Current.MainWindow;
            if (window is null || !window.IsVisible) return;

            SetCurrent(color);
        });
    }

    private void OnCaptured(object? sender, CapturedColor color)
    {
        _captureFormatOverride = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() => SetCurrent(color.ToRgb()));
    }

    /// <summary>1/2/3 was pressed mid-capture: re-render the last hovered color in that format
    /// right away, instead of waiting for the next mouse move.</summary>
    private void OnActiveFormatChanged(object? sender, CopyFormat format)
    {
        _captureFormatOverride = format;
        System.Windows.Application.Current.Dispatcher.Invoke(() => SetCurrent(_currentColor));
    }

    private void CopyWithToast(string text)
    {
        if (_clipboard.TrySetText(text))
            _notifications.ShowCaptureConfirmation(_currentColor.ToHex(), text);
    }

    /// <summary>Recomputes all four format readouts for the given color against the current
    /// default format setting. Also called after the settings window closes, so switching the
    /// default format is reflected immediately without needing a new capture/hover.</summary>
    public void SetCurrent(RgbColor color)
    {
        _currentColor = color;

        // Frozen: an unfrozen brush keeps change-notification plumbing and thread affinity alive
        // for no benefit — this one is replaced, never mutated.
        var brush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        CurrentBrush = brush;

        var defaultFormat = _captureFormatOverride ?? _settingsAccessor().CopyFormat;
        PrimaryFormat = BuildEntry(defaultFormat, color, isDefault: true);

        // Replace entries in place instead of Clear+Add: this runs on every hover mouse move,
        // and clearing forces the ItemsControl to drop and regenerate its containers each time.
        // Records compare by value, so unchanged entries are skipped entirely.
        var index = 0;
        foreach (var format in AllFormats)
        {
            if (format == defaultFormat) continue;

            var entry = BuildEntry(format, color, isDefault: false);
            if (index < SecondaryFormats.Count)
            {
                if (SecondaryFormats[index] != entry)
                    SecondaryFormats[index] = entry;
            }
            else
            {
                SecondaryFormats.Add(entry);
            }
            index++;
        }

        while (SecondaryFormats.Count > index)
            SecondaryFormats.RemoveAt(SecondaryFormats.Count - 1);
    }

    private static ColorFormatEntry BuildEntry(CopyFormat format, RgbColor color, bool isDefault) =>
        new(format, format.ToLabel(), color.Format(format), isDefault);

    private void RefreshHistory()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            History.Clear();
            MoreColors.Clear();
            var items = _history.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (i < MainSwatchCount) History.Add(items[i]);
                else MoreColors.Add(items[i]);
            }

            HasMoreColors = MoreColors.Count > 0;
        });
    }
}

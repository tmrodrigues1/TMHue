using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;

namespace TMHue.App.Views;

/// <summary>
/// Small always-on-top lens that mirrors the pixels around the cursor during a capture, so the
/// exact target pixel can be lined up against anti-aliasing or compression noise. Click-through
/// and never activates, so it can float on top of the picker overlay without stealing its mouse
/// capture or focus.
/// </summary>
public partial class MagnifierWindow : Window
{
    public CaptureExclusionResult CaptureExclusionStatus { get; private set; }
    public const int RegionSide = 11;

    // One reusable 11x11 bitmap scaled up by the Image element; writing 121 ints into it per
    // frame is orders of magnitude cheaper than restyling 121 WPF shapes.
    private readonly WriteableBitmap _bitmap = new(RegionSide, RegionSide, 96, 96, PixelFormats.Bgr32, null);
    private readonly int[] _pixelBuffer = new int[RegionSide * RegionSide];

    private int _physicalWidth;
    private int _physicalHeight;
    private bool _hasPixels;
    private string? _monitorDevice;
    private int _sampleSide;
    private (int X, int Y, int Width, int Height)? _placement;

    // Monitors are fixed for the lens' lifetime: a topology change ends the capture.
    private readonly MonitorLayout _layout;

    // UpdateContent/MoveTo run on every picker frame. The readout, the source line and the
    // physical size are only recomputed when their inputs actually changed.
    private (RgbColor Color, CopyFormat Format)? _shownReadout;
    private (CaptureColorOutput Output, bool Hdr, bool Frozen)? _shownSource;
    private bool _sizeStale = true;
    internal int BitmapUpdateCount { get; private set; }
    internal string DisplayedValue => HexLabel.Text;

    public MagnifierWindow(MonitorLayout? layout = null)
    {
        _layout = layout ?? new MonitorLayout();
        InitializeComponent();
        PixelImage.Source = _bitmap;

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            OverlayPlacement.MakeClickThroughAndNonActivating(handle);
            CaptureExclusionStatus = CaptureExclusion.Apply(handle);
        };

        // The lens has a fixed layout; measuring once avoids an ActualWidth/DPI computation on
        // every mouse move.
        Loaded += (_, _) =>
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _physicalWidth = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
            _physicalHeight = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        };
    }

    private static readonly (CopyFormat Format, string Key, string Label)[] FormatShortcuts =
    {
        (CopyFormat.Hex, "1", "HEX"),
        (CopyFormat.Rgb, "2", "RGB"),
        (CopyFormat.Hsl, "3", "HSL")
    };

    /// <summary>Renders the centered <see cref="RegionSide"/>² sub-square of a region that may be
    /// larger than the lens (the coordinator captures one region sized for both the lens and the
    /// user's sampling area, instead of one screen capture each).</summary>
    public void UpdateContent(ScreenSample sample, RgbColor centerColor, CopyFormat activeFormat, int sampleSide)
    {
        var changed = !_hasPixels;
        var offset = (sample.Side - RegionSide) / 2;
        for (var row = 0; row < RegionSide; row++)
        {
            for (var col = 0; col < RegionSide; col++)
            {
                var c = sample.Pixels[offset + row, offset + col];
                var packed = sample.Valid[offset + row, offset + col]
                    ? (c.Red << 16) | (c.Green << 8) | c.Blue
                    : ((row + col) % 2 == 0 ? 0x454550 : 0x252530);
                var index = row * RegionSide + col;
                changed |= _pixelBuffer[index] != packed;
                _pixelBuffer[index] = packed;
            }
        }

        if (changed)
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, RegionSide, RegionSide), _pixelBuffer, RegionSide * 4, 0);
            BitmapUpdateCount++;
            _hasPixels = true;
        }

        ShowReadout(centerColor, activeFormat);
        if (_sampleSide != sampleSide)
        {
            _sampleSide = sampleSide;
            _sizeStale = true;
            SampleBoundary.Width = SampleBoundary.Height = sampleSide * 12;
            SampleOutline.Width = SampleOutline.Height = sampleSide * 12;
            SampleLabel.Text = TMHue.App.Infrastructure.LocalizationService.Get(sampleSide switch
            { 5 => "L.Settings.Sample5", 11 => "L.Settings.Sample11", _ => "L.Settings.SampleExact" });
        }
        var source = (sample.ColorOutput, sample.AdvancedColorActive is true, sample.IsFrozen);
        if (_shownSource != source)
        {
            _shownSource = source;
            var sourceKey = sample.ColorOutput == CaptureColorOutput.Srgb ? "L.Magnifier.SrgbSource"
                : sample.AdvancedColorActive is true ? "L.Magnifier.HdrSource" : "L.Magnifier.RenderedSource";
            var description = TMHue.App.Infrastructure.LocalizationService.Get(sourceKey);
            if (sample.IsFrozen) description += " · " + TMHue.App.Infrastructure.LocalizationService.Get("L.Magnifier.Frozen");
            SourceLabel.Text = description;
            _sizeStale = true;
        }
        UpdateFormatHint(activeFormat);
    }

    private void ShowReadout(RgbColor color, CopyFormat format)
    {
        if (_shownReadout == (color, format)) return;
        _shownReadout = (color, format);
        HexLabel.Text = color.Format(format);
        _sizeStale = true;
    }

    /// <summary>Updates just the readout text and hint when the format shortcut changes
    /// mid-hover, without re-writing the pixel bitmap.</summary>
    public void UpdateFormat(RgbColor centerColor, CopyFormat activeFormat)
    {
        ShowReadout(centerColor, activeFormat);
        UpdateFormatHint(activeFormat);
    }

    private static readonly System.Windows.Media.Brush SeparatorBrush = CreateFrozenBrush(0x6F, 0x72, 0x90);
    private static readonly System.Windows.Media.Brush InactiveBrush = CreateFrozenBrush(0xA6, 0xA9, 0xC6);

    private static System.Windows.Media.Brush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    // UpdateContent runs on every mouse move; rebuilding the hint's Runs each time would churn
    // WPF objects and layout hundreds of times per second for text that only changes when the
    // user presses 1/2/3. Cache the last rendered format and skip the rebuild when unchanged.
    private CopyFormat? _renderedHintFormat;

    /// <summary>Renders the "1 HEX · 2 RGB · 3 HSL" hint below the color readout, bolding
    /// whichever format is currently active so the shortcut being used is obvious at a glance.</summary>
    private void UpdateFormatHint(CopyFormat activeFormat)
    {
        if (_renderedHintFormat == activeFormat) return;
        _renderedHintFormat = activeFormat;
        _sizeStale = true;

        FormatHint.Inlines.Clear();

        for (var i = 0; i < FormatShortcuts.Length; i++)
        {
            var (format, key, label) = FormatShortcuts[i];
            var isActive = format == activeFormat;

            if (i > 0)
                FormatHint.Inlines.Add(new Run("  ·  ") { Foreground = SeparatorBrush });

            var run = new Run($"{key} {label}")
            {
                Foreground = isActive ? System.Windows.Media.Brushes.White : InactiveBrush,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal
            };
            FormatHint.Inlines.Add(run);
        }
    }

    /// <summary>Positions the lens near the cursor in physical pixels, flipping to the opposite
    /// side of the cursor when it would otherwise run off the virtual desktop.</summary>
    public void MoveTo(int physicalX, int physicalY)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero || _physicalWidth == 0) return;

        if (_layout.At(physicalX, physicalY) is not { } monitor) return;
        var bounds = monitor.Bounds;
        if (_monitorDevice != monitor.DeviceName)
        {
            _monitorDevice = monitor.DeviceName;
            OverlayPlacement.PlaceAt(hwnd, bounds.Left + 24, bounds.Top + 24);
            _sizeStale = true;
        }
        if (_sizeStale)
        {
            _sizeStale = false;
            UpdateLayout();
            var scale = OverlayPlacement.GetWindowScale(hwnd);
            _physicalWidth = (int)Math.Ceiling(ActualWidth * scale);
            _physicalHeight = (int)Math.Ceiling(ActualHeight * scale);
        }
        var position = bounds.PlaceLens(physicalX, physicalY, _physicalWidth, _physicalHeight);
        var placement = (position.X, position.Y, _physicalWidth, _physicalHeight);
        if (_placement != placement)
        {
            _placement = placement;
            OverlayPlacement.PlaceAt(hwnd, position.X, position.Y);
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _physicalWidth = (int)Math.Ceiling(ActualWidth * newDpi.DpiScaleX);
        _physicalHeight = (int)Math.Ceiling(ActualHeight * newDpi.DpiScaleY);
        _sizeStale = true;
    }
}

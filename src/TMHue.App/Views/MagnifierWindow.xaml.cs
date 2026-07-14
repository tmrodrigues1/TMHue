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
    public const int RegionSide = 11;

    // One reusable 11x11 bitmap scaled up by the Image element; writing 121 ints into it per
    // frame is orders of magnitude cheaper than restyling 121 WPF shapes.
    private readonly WriteableBitmap _bitmap = new(RegionSide, RegionSide, 96, 96, PixelFormats.Bgr32, null);
    private readonly int[] _pixelBuffer = new int[RegionSide * RegionSide];

    private int _physicalWidth;
    private int _physicalHeight;

    public MagnifierWindow()
    {
        InitializeComponent();
        PixelImage.Source = _bitmap;

        SourceInitialized += (_, _) =>
            OverlayPlacement.MakeClickThroughAndNonActivating(new WindowInteropHelper(this).Handle);

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
    public void UpdateContent(RgbColor[,] region, int regionSide, RgbColor centerColor, CopyFormat activeFormat)
    {
        var offset = (regionSide - RegionSide) / 2;
        for (var row = 0; row < RegionSide; row++)
        {
            for (var col = 0; col < RegionSide; col++)
            {
                var c = region[offset + row, offset + col];
                _pixelBuffer[row * RegionSide + col] = (c.Red << 16) | (c.Green << 8) | c.Blue;
            }
        }

        _bitmap.WritePixels(
            new Int32Rect(0, 0, RegionSide, RegionSide),
            _pixelBuffer,
            RegionSide * 4,
            0);

        HexLabel.Text = centerColor.Format(activeFormat);
        UpdateFormatHint(activeFormat);
    }

    /// <summary>Updates just the readout text and hint when the format shortcut changes
    /// mid-hover, without re-writing the pixel bitmap.</summary>
    public void UpdateFormat(RgbColor centerColor, CopyFormat activeFormat)
    {
        HexLabel.Text = centerColor.Format(activeFormat);
        UpdateFormatHint(activeFormat);
    }

    private static readonly System.Windows.Media.Brush SeparatorBrush = CreateFrozenBrush(0x6A, 0x69, 0x76);
    private static readonly System.Windows.Media.Brush InactiveBrush = CreateFrozenBrush(0x9A, 0x99, 0xA6);

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

        var bounds = VirtualScreenBounds.GetCurrent();

        const int offset = 24;
        var x = physicalX + offset;
        var y = physicalY + offset;

        if (x + _physicalWidth > bounds.Right) x = physicalX - offset - _physicalWidth;
        if (y + _physicalHeight > bounds.Bottom) y = physicalY - offset - _physicalHeight;

        OverlayPlacement.PlaceAt(hwnd, x, y);
    }
}

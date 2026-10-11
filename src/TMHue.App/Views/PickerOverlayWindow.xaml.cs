using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TMHue.Windows.Display;
using TMHue.Core.Models;

namespace TMHue.App.Views;

/// <summary>
/// Transparent, borderless, topmost window covering one monitor (or the virtual desktop in diagnostics). It exists to
/// receive mouse events during a capture. Its alpha-1 hit-test surface is excluded from capture.
/// </summary>
public partial class PickerOverlayWindow : Window
{
    public CaptureExclusionResult CaptureExclusionStatus { get; private set; }
    public event EventHandler<(int PhysicalX, int PhysicalY)>? PointerMoved;
    public event EventHandler<(int PhysicalX, int PhysicalY)>? PointerClicked;
    public event EventHandler? CancelRequested;
    public event EventHandler? ReadFailed;
    private HwndSource? _source;
    private readonly PixelBounds? _monitorBounds;
    private bool _placing;
    public event EventHandler<TMHue.Core.Models.CopyFormat>? FormatKeyPressed;

    /// <summary>Raised on Left/Right/Up/Down while picking, so the cursor can be nudged with
    /// pixel precision instead of relying on the mouse. Shift held asks the coordinator to jump
    /// to the next detected color change instead of moving by a single pixel.</summary>
    public event EventHandler<(int Dx, int Dy, bool JumpToColorChange)>? ArrowKeyPressed;

    public PickerOverlayWindow() : this(null) { }

    public PickerOverlayWindow(PixelBounds? monitorBounds)
    {
        _monitorBounds = monitorBounds;
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            CaptureExclusionStatus = CaptureExclusion.Apply(handle);
            _source = HwndSource.FromHwnd(handle);
            _source?.AddHook(OnNativeMessage);
            ApplyMonitorBounds();
        };
        Closed += (_, _) => { _source?.RemoveHook(OnNativeMessage); _source = null; };

        // Sized in WPF's own DIP coordinate space (not raw Win32 pixels) so the window's hit-test
        // area exactly matches its visible/native bounds. Mixing SetWindowPos-in-physical-pixels
        // with WPF's layout system was the root cause of the picker only responding while the
        // cursor stayed near the app window: the native HWND covered the desktop, but WPF's own
        // layout/hit-testing rectangle did not, so clicks outside that mismatch reached whatever
        // real window was underneath instead of this overlay.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += OnLoaded;
        MouseMove += (_, _) => RaiseFromCursor(PointerMoved);
        MouseRightButtonDown += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        // PreviewKeyDown (tunneling) instead of KeyDown: Escape must cancel the capture even if
        // keyboard focus drifted to a child element or the bubbling event gets handled elsewhere.
        PreviewKeyDown += OnKeyDown;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (IsLoaded && !System.Windows.Application.Current.Windows.OfType<PickerOverlayWindow>().Any(w => w.IsActive))
                CancelRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    private nint OnNativeMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x007E) ReadFailed?.Invoke(this, EventArgs.Empty); // display topology changed
        if (message is not (0x0201 or 0x0203)) return nint.Zero; // left down/double click
        handled = true;
        if (MouseMessagePosition.TryGetScreenPosition(hwnd, lParam, out var position))
            PointerClicked?.Invoke(this, position);
        else
            ReadFailed?.Invoke(this, EventArgs.Empty);
        return nint.Zero;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Defensive re-assertion of top-most z-order only (no position/size change, which WPF
        // already owns via Left/Top/Width/Height above).
        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayPlacement.ForceTopmost(hwnd);
        ApplyMonitorBounds();
        if (ShowActivated)
        {
            Activate(); Focus(); Keyboard.Focus(this);
        }
    }

    public void SetFrozenImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        FrozenImage.Source = image;
        FrozenImage.Visibility = Visibility.Visible;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyMonitorBounds();
    }

    private void ApplyMonitorBounds()
    {
        if (_placing || _monitorBounds is not { } bounds) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        _placing = true;
        try
        {
            OverlayPlacement.PlaceMonitorWindow(handle, bounds);
            var scale = OverlayPlacement.GetWindowScale(handle);
            Width = bounds.Width / scale; Height = bounds.Height / scale;
            OverlayPlacement.PlaceMonitorWindow(handle, bounds);
        }
        finally { _placing = false; }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        var format = e.Key switch
        {
            Key.D1 or Key.NumPad1 => TMHue.Core.Models.CopyFormat.Hex,
            Key.D2 or Key.NumPad2 => TMHue.Core.Models.CopyFormat.Rgb,
            Key.D3 or Key.NumPad3 => TMHue.Core.Models.CopyFormat.Hsl,
            _ => (TMHue.Core.Models.CopyFormat?)null
        };

        if (format is { } chosen)
        {
            FormatKeyPressed?.Invoke(this, chosen);
            return;
        }

        (int Dx, int Dy)? direction = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => null
        };

        if (direction is { } d)
        {
            var shiftHeld = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            ArrowKeyPressed?.Invoke(this, (d.Dx, d.Dy, shiftHeld));
            e.Handled = true;
        }
    }

    private void RaiseFromCursor(EventHandler<(int PhysicalX, int PhysicalY)>? handler)
    {
        try
        {
            var (x, y) = OverlayPlacement.GetPhysicalCursorPosition();
            handler?.Invoke(this, (x, y));
        }
        catch (System.ComponentModel.Win32Exception) { ReadFailed?.Invoke(this, EventArgs.Empty); }
    }
}

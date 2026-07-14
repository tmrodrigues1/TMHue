using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TMHue.Windows.Display;

namespace TMHue.App.Views;

/// <summary>
/// Transparent, borderless, topmost window spanning the full virtual desktop. It only exists to
/// receive mouse events during a capture; it never paints anything visible itself.
/// </summary>
public partial class PickerOverlayWindow : Window
{
    public event EventHandler<(int PhysicalX, int PhysicalY)>? PointerMoved;
    public event EventHandler<(int PhysicalX, int PhysicalY)>? PointerClicked;
    public event EventHandler? CancelRequested;
    public event EventHandler<TMHue.Core.Models.CopyFormat>? FormatKeyPressed;

    /// <summary>Raised on Left/Right/Up/Down while picking, so the cursor can be nudged with
    /// pixel precision instead of relying on the mouse. Shift held asks the coordinator to jump
    /// to the next detected color change instead of moving by a single pixel.</summary>
    public event EventHandler<(int Dx, int Dy, bool JumpToColorChange)>? ArrowKeyPressed;

    public PickerOverlayWindow()
    {
        InitializeComponent();

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
        MouseLeftButtonDown += (_, _) => RaiseFromCursor(PointerClicked);
        MouseRightButtonDown += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        // PreviewKeyDown (tunneling) instead of KeyDown: Escape must cancel the capture even if
        // keyboard focus drifted to a child element or the bubbling event gets handled elsewhere.
        PreviewKeyDown += OnKeyDown;
        Deactivated += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Defensive re-assertion of top-most z-order only (no position/size change, which WPF
        // already owns via Left/Top/Width/Height above).
        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayPlacement.ForceTopmost(hwnd);

        Activate();
        Focus();
        Keyboard.Focus(this);
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

    private static void RaiseFromCursor(EventHandler<(int PhysicalX, int PhysicalY)>? handler)
    {
        var (x, y) = OverlayPlacement.GetPhysicalCursorPosition();
        handler?.Invoke(null, (x, y));
    }
}

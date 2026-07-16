using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TMHue.Windows.Display;

namespace TMHue.App.Views;

/// <summary>
/// Full-virtual-desktop overlay for selecting a screen region to extract a palette from.
/// The visual rubber band is drawn in WPF DIP coordinates; the returned <see cref="Selection"/>
/// is recorded in physical pixels (via GetPhysicalCursorPos) so the subsequent CopyFromScreen is
/// DPI-correct on mixed-DPI monitor setups.
/// </summary>
public partial class RegionCaptureWindow : Window
{
    private System.Windows.Point _dragStartDip;
    private (int X, int Y) _dragStartPhysical;
    private bool _dragging;

    /// <summary>Selected region in physical pixels, or null if the user cancelled.</summary>
    public (int X, int Y, int Width, int Height)? Selection { get; private set; }

    public RegionCaptureWindow()
    {
        InitializeComponent();

        // Same DIP-coordinate sizing rationale as PickerOverlayWindow: WPF must own the bounds
        // so its hit-testing rectangle matches the native window across the whole desktop.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        MouseRightButtonDown += (_, _) => Close();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayPlacement.ForceTopmost(hwnd);
        Activate();
        Focus();
        Keyboard.Focus(this);

        // Centers the hint pill near the top of the primary monitor's area of the overlay.
        HintPill.UpdateLayout();
        Canvas.SetLeft(HintPill, -Left + (SystemParameters.PrimaryScreenWidth - HintPill.ActualWidth) / 2);
        Canvas.SetTop(HintPill, -Top + 32);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStartDip = e.GetPosition(SelectionCanvas);
        _dragStartPhysical = OverlayPlacement.GetPhysicalCursorPosition();
        SelectionRect.Visibility = Visibility.Visible;
        HintPill.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging) return;

        var current = e.GetPosition(SelectionCanvas);
        Canvas.SetLeft(SelectionRect, Math.Min(_dragStartDip.X, current.X));
        Canvas.SetTop(SelectionRect, Math.Min(_dragStartDip.Y, current.Y));
        SelectionRect.Width = Math.Abs(current.X - _dragStartDip.X);
        SelectionRect.Height = Math.Abs(current.Y - _dragStartDip.Y);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var end = OverlayPlacement.GetPhysicalCursorPosition();
        var x = Math.Min(_dragStartPhysical.X, end.X);
        var y = Math.Min(_dragStartPhysical.Y, end.Y);
        var width = Math.Abs(end.X - _dragStartPhysical.X);
        var height = Math.Abs(end.Y - _dragStartPhysical.Y);

        // A click without a real drag (or a sliver) isn't a usable region; treat it as cancel.
        if (width >= 4 && height >= 4)
            Selection = (x, y, width, height);

        Close();
    }
}

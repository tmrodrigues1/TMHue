using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TMHue.App.Views;

/// <summary>
/// Borderless, topmost, click-through confirmation shown centered on screen after a successful
/// capture — visible over any application, not just the TMHue window. Auto-dismisses itself.
/// </summary>
public partial class CaptureToastWindow : Window
{
    private const double VisibleMilliseconds = 1800;
    private const double FadeMilliseconds = 220;

    public CaptureToastWindow(string hex, string? displayText = null)
    {
        InitializeComponent();
        HexText.Text = displayText ?? hex;
        try
        {
            var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            SwatchDot.Background = brush;
        }
        catch
        {
            // keep the default dot color if the hex is ever malformed
        }
        Opacity = 0;

        Loaded += (_, _) =>
        {
            PlaceAboveTaskbar();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(VisibleMilliseconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(FadeMilliseconds));
                fadeOut.Completed += (_, _) => Close();
                BeginAnimation(OpacityProperty, fadeOut);
            };
            timer.Start();
        };
    }

    /// <summary>Bottom-center of the work area: WorkArea already excludes the taskbar, so the
    /// toast sits horizontally centered a small gap above it.</summary>
    private void PlaceAboveTaskbar()
    {
        // The window already carries a 24px transparent margin for its shadow, which doubles
        // as the visual gap to the taskbar.
        const double gapAboveTaskbar = 4;
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;
        Top = SystemParameters.WorkArea.Bottom - ActualHeight - gapAboveTaskbar;
    }
}

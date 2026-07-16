using System.Windows;
using System.Windows.Input;
using TMHue.App.Infrastructure;
using TMHue.App.ViewModels;

namespace TMHue.App.Views;

public partial class HarmonyWindow : Window
{
    private HarmonyViewModel ViewModel => (HarmonyViewModel)DataContext;

    public HarmonyWindow(HarmonyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Releases a pending eyedropper subscription (and cancels an in-flight capture) if the
        // window is closed before the pick completes — same contract as the contrast checker.
        Closed += (_, _) => viewModel.Dispose();

        PreviewKeyDown += OnPreviewKeyDown;
    }

    // Esc closes the modal, but backs off while an eyedropper capture is in progress: the
    // system-wide Esc hotkey already owns cancelling that capture.
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (ViewModel.Coordinator.State != PickerState.Idle) return;

        e.Handled = true;
        OnCloseClick(this, new RoutedEventArgs());
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    // Deferred close for the same borderless-over-owner click-fallthrough reason documented in
    // ContrastCheckerWindow.OnCloseClick.
    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
}

using System.Windows;
using System.Windows.Input;
using TMHue.App.Infrastructure;
using TMHue.App.ViewModels;
using TMHue.Core.Models;

namespace TMHue.App.Views;

public partial class ContrastCheckerWindow : Window
{
    public event EventHandler? SettingsRequested;

    private ContrastCheckerViewModel ViewModel => (ContrastCheckerViewModel)DataContext;

    public ContrastCheckerWindow(ContrastCheckerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Releases the pending eyedropper subscription (and cancels an in-flight capture this
        // view model itself started) if the window is closed before a capture completes.
        Closed += (_, _) => viewModel.Dispose();

        PreviewKeyDown += OnPreviewKeyDown;
    }

    // Esc closes the modal, same as clicking its close button. While an eyedropper capture is
    // in progress, the system-wide Esc hotkey (registered in ColorPickerCoordinator) already
    // owns cancelling that capture, so this window's own Esc handler backs off to avoid closing
    // itself as a side effect of the user just cancelling the picker.
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

    // This window is borderless/transparent and centered almost exactly over its owner (the
    // main window, which is nearly the same size). Calling Close() synchronously inside the
    // Click handler can let the same mouse-up "fall through" to whatever sits at the same screen
    // position on the owner the instant this window disappears mid-message. Deferring the actual
    // close to the next dispatcher tick lets input processing for this click finish first.
    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnHexPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !ViewModel.AllowedColorCharacters.IsMatch(e.Text);
    }

    private void OnHexPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)) ||
            e.DataObject.GetData(typeof(string)) is not string pasted ||
            !ViewModel.AllowedColorCharacters.IsMatch(pasted))
        {
            e.CancelCommand();
        }
    }
}

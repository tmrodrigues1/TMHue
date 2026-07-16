using System.Windows;
using System.Windows.Input;
using TMHue.App.ViewModels;
using TMHue.Windows.Hotkeys;

namespace TMHue.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    // Same borderless/near-owner-sized click-through mitigation as ContrastCheckerWindow: defer
    // the actual close so this click's mouse-up doesn't fall through onto the owner window.
    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private async void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            await viewModel.CheckForUpdatesAsync();
    }

    private void OnDeclineUpdateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            viewModel.DeclineUpdate();
    }

    /// <summary>The user consented: run the download/apply in a dedicated progress modal. On
    /// success Velopack restarts the app (this never returns); on failure the offer stays on
    /// screen so they can try again.</summary>
    private void OnAcceptUpdateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var progress = new UpdateProgressWindow(viewModel.UpdateService) { Owner = this };
        progress.ShowDialog();

        if (!progress.Succeeded)
            viewModel.ReportUpdateFailed();
    }

    private void OnChangeHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        if (sender is not System.Windows.Controls.Button { Tag: string hotkeyId })
            return;

        viewModel.BeginCaptureHotkey(hotkeyId);
        Focus();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (DataContext is not SettingsViewModel viewModel || viewModel.CapturingHotkeyId is not { } hotkeyId)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (HotkeyKeyName.IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Escape)
        {
            viewModel.CancelCaptureHotkey();
            e.Handled = true;
            return;
        }

        e.Handled = true;

        var modifiers = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Add("Control");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers.Add("Windows");

        var keyName = HotkeyKeyName.From(key);
        if (modifiers.Count == 0 || keyName is null)
            return;

        viewModel.TrySetHotkey(hotkeyId, modifiers, keyName);
    }
}

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
        Closed += (_, _) => viewModel.CancelCaptureHotkey();
        SizeChanged += (_, _) => KeepWithinWorkingArea();
        Loaded += (_, _) =>
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            MaxHeight = Math.Max(200, screen.WorkingArea.Height / dpi.DpiScaleY - 24);
            KeepWithinWorkingArea();
        };
    }

    private void KeepWithinWorkingArea()
    {
        if (!IsLoaded) return;
        var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var origin = PointToScreen(new System.Windows.Point(0, 0));
        var minimumTop = screen.WorkingArea.Top + 12;
        var maximumTop = Math.Max(minimumTop, screen.WorkingArea.Bottom - ActualHeight * dpi.DpiScaleY - 12);
        var targetTop = Math.Clamp(origin.Y, minimumTop, maximumTop);
        if (Math.Abs(targetTop - origin.Y) >= 1)
            Top += (targetTop - origin.Y) / dpi.DpiScaleY;
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

    private void OnChangeHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        if (sender is not System.Windows.Controls.Button { Tag: string hotkeyId })
            return;

        viewModel.BeginCaptureHotkey(hotkeyId);
        Focus();
    }

    private void OnSettingsSectionExpanded(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender)) return;
        if (ReferenceEquals(sender, AdvancedOptionsSection))
            GlobalHotkeysSection.IsExpanded = false;
        else if (ReferenceEquals(sender, GlobalHotkeysSection))
            AdvancedOptionsSection.IsExpanded = false;
    }

    private void OnHotkeysCollapsed(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            viewModel.CancelCaptureHotkey();
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

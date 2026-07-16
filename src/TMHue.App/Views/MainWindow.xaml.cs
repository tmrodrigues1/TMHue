using System.Windows;
using System.Windows.Input;
using TMHue.App.ViewModels;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;

namespace TMHue.App.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly AppSettings _settings;
    private bool _isExiting;

    // LocationChanged fires for every pixel of a window drag; saving settings (JSON serialize +
    // disk write, on a OneDrive-synced folder) that often is pure waste. The timer restarts on
    // each move and only persists once the window has been still for a moment.
    private readonly System.Windows.Threading.DispatcherTimer _placementSaveTimer =
        new() { Interval = TimeSpan.FromMilliseconds(500) };

    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ContrastCheckerRequested;
    public event EventHandler? HarmonyRequested;
    public event EventHandler? PaletteExtractorRequested;

    public MainWindow(MainViewModel viewModel, ISettingsRepository settingsRepository, AppSettings settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsRepository = settingsRepository;
        _settings = settings;

        RestorePlacement();

        Closing += OnClosing;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                MinimizeToTray();
        };
        _placementSaveTimer.Tick += (_, _) =>
        {
            _placementSaveTimer.Stop();
            PersistPlacement();
        };
        LocationChanged += (_, _) =>
        {
            _placementSaveTimer.Stop();
            _placementSaveTimer.Start();
        };

        // SizeToContent + AllowsTransparency has a long-standing WPF bug: when the "Ver mais"
        // sidebar flips from Collapsed to Visible, the window sometimes keeps its old width and
        // the panel is rendered clipped outside it (intermittently, depending on render timing).
        // Re-asserting SizeToContent after layout settles forces the window to re-measure, so the
        // sidebar always shows.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.IsMorePanelOpen)) return;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                SizeToContent = SizeToContent.Manual;
                InvalidateMeasure();
                UpdateLayout();
                SizeToContent = SizeToContent.WidthAndHeight;
            }));
        };
    }

    /// <summary>Called by the tray "Sair" menu item to allow the window to actually close.</summary>
    public void AllowExit()
    {
        _isExiting = true;
        Close();
    }

    // Only the position is persisted/restored: the window is a fixed-size, non-resizable card.
    private void RestorePlacement()
    {
        if (_settings.WindowLeft is { } left && _settings.WindowTop is { } top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void PersistPlacement()
    {
        if (WindowState != WindowState.Normal) return;

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settingsRepository.Save(_settings);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Flush a pending (debounced) placement save so a drag immediately followed by a
        // close/hide never loses the final position.
        if (_placementSaveTimer.IsEnabled)
        {
            _placementSaveTimer.Stop();
            PersistPlacement();
        }

        if (_isExiting || !_settings.CloseToTray) return;

        e.Cancel = true;
        Hide();
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    /// <summary>Minimizing sends the app to the system tray instead of the taskbar. The window
    /// state is reset to Normal before hiding so reopening from the tray restores it visibly.</summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => MinimizeToTray();

    private void MinimizeToTray()
    {
        WindowState = WindowState.Normal;
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnContrastCheckerClick(object sender, RoutedEventArgs e) => ContrastCheckerRequested?.Invoke(this, EventArgs.Empty);

    private void OnHarmonyClick(object sender, RoutedEventArgs e) => HarmonyRequested?.Invoke(this, EventArgs.Empty);

    private void OnPaletteExtractorClick(object sender, RoutedEventArgs e) => PaletteExtractorRequested?.Invoke(this, EventArgs.Empty);

    private void OnAuthorLinkClick(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}

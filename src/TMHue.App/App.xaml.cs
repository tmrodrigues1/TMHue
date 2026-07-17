using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TMHue.App.Infrastructure;
using TMHue.App.ViewModels;
using TMHue.App.Views;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Windows.Clipboard;
using TMHue.Windows.Cursor;
using TMHue.Windows.Hotkeys;
using TMHue.Windows.Notifications;
using TMHue.Windows.Persistence;
using TMHue.Windows.Sampling;
using TMHue.Windows.Startup;

namespace TMHue.App;

public partial class App : System.Windows.Application
{
    private const long MaxErrorLogBytes = 1024 * 1024;
    private const int MaxErrorLogEntryCharacters = 16 * 1024;

    private ServiceProvider? _services;
    private SingleInstanceService? _singleInstance;
    private TrayIconService? _trayIcon;
    private IGlobalHotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private AppSettings _settings = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A tray-resident utility must never die silently on a UI exception (e.g. a bad resource
        // while rendering a toast). Log and keep running.
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                Directory.CreateDirectory(AppPaths.LogsFolder);
                AppendErrorLog(args.Exception);
            }
            catch
            {
                // logging must never crash the handler
            }
            args.Handled = true;
        };

        var startMinimized = e.Args.Contains("--minimized");

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire())
        {
            SingleInstanceService.NotifyRunningInstance(
                e.Args.Contains("--capture") ? SecondInstanceAction.StartCapture
                : startMinimized ? SecondInstanceAction.BringToFront
                : SecondInstanceAction.OpenWindow);
            Shutdown();
            return;
        }

        _singleInstance.SecondInstanceRequested += OnSecondInstanceRequested;

        MigrateLegacyDataFolder();
        Directory.CreateDirectory(AppPaths.RootFolder);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        _settings = _services.GetRequiredService<AppSettings>();
        ReconcileStartupRegistration();

        var themeService = _services.GetRequiredService<IThemeService>();
        themeService.Apply(_settings.Theme);

        LocalizationService.Apply(_settings.Language);
        LocalizationService.LanguageChanged += (_, _) => _trayIcon?.SetLabels(BuildTrayLabels());

        var history = _services.GetRequiredService<IColorHistoryService>();
        history.Load();

        _mainWindow = _services.GetRequiredService<MainWindow>();
        _mainWindow.SettingsRequested += (_, _) => OpenSettings();
        _mainWindow.ExitRequested += (_, _) => ExitApplication();
        _mainWindow.ContrastCheckerRequested += (_, _) => OpenContrastChecker();
        _mainWindow.HarmonyRequested += (_, _) => OpenHarmonyGenerator();
        _mainWindow.PaletteExtractorRequested += (_, _) => OpenPaletteExtractor();

        SetupTray();
        SetupHotkey();

        if (!startMinimized)
            _mainWindow.Show();

        SetupUpdates();
    }

    /// <summary>Checagem de atualização só depois da UI pronta, em segundo plano, no máximo
    /// uma vez a cada 24 h (o UpdateService aplica o throttle e o timeout). Nova versão gera
    /// um toast com botão "Atualizar"; nada é baixado sem clique explícito.</summary>
    private void SetupUpdates()
    {
        var updateService = _services!.GetRequiredService<UpdateService>();
        updateService.UpdateAvailable += (_, version) =>
        {
            var toast = new UpdateToastWindow(updateService, version);
            toast.Show();
        };
        updateService.CheckInBackground();
    }

    /// <summary>The app was renamed from "TMHue" to "TMHue"; the settings/history folder
    /// moved with it. A one-time move preserves existing users' data instead of resetting them
    /// to defaults on first launch after the update.</summary>
    private static void MigrateLegacyDataFolder()
    {
        try
        {
            var legacyFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TMHue");

            if (Directory.Exists(legacyFolder) && !Directory.Exists(AppPaths.RootFolder))
                Directory.Move(legacyFolder, AppPaths.RootFolder);
        }
        catch
        {
            // Best-effort; must never block startup. Worst case, settings reset to defaults.
        }
    }

    /// <summary>The Run-key check compares an exact string, so a moved/updated executable or a
    /// Run entry cleared by another tool would silently leave "Iniciar com o Windows" not
    /// actually working while still showing as enabled. Re-applying the desired state on every
    /// launch keeps the registry entry honest.</summary>
    private void ReconcileStartupRegistration()
    {
        var startupService = _services!.GetRequiredService<IStartupService>();
        if (startupService.IsEnabled != _settings.StartWithWindows)
            startupService.SetEnabled(_settings.StartWithWindows);
    }

    private void ConfigureServices(ServiceCollection services)
    {
        var settingsRepository = new SettingsRepository(AppPaths.SettingsFile);
        var settings = settingsRepository.Load();

        services.AddSingleton<ISettingsRepository>(settingsRepository);
        services.AddSingleton(settings);
        services.AddSingleton<Func<AppSettings>>(sp => () => sp.GetRequiredService<AppSettings>());

        services.AddSingleton<IColorHistoryService>(new ColorHistoryService(AppPaths.HistoryFile));
        services.AddSingleton<IScreenColorSampler, ScreenColorSampler>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IStartupService>(new StartupService(Environment.ProcessPath ?? AppContext.BaseDirectory));
        services.AddSingleton(new CursorService(AppPaths.CursorResourcePath));
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();

        services.AddSingleton<INotificationService, ToastNotificationService>();

        services.AddSingleton(sp => new UpdateService(
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<ISettingsRepository>(),
            LogErrorSafe));

        services.AddSingleton(sp => new ColorPickerCoordinator(
            sp.GetRequiredService<IScreenColorSampler>(),
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IColorHistoryService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<CursorService>(),
            sp.GetRequiredService<IGlobalHotkeyService>(),
            () => sp.GetRequiredService<AppSettings>()));

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private void SetupTray()
    {
        var icon = LoadTrayIcon();
        _trayIcon = new TrayIconService(icon, BuildTrayLabels());

        _trayIcon.OpenRequested += (_, _) => ShowMainWindow();
        _trayIcon.CaptureRequested += (_, _) => _services!.GetRequiredService<ColorPickerCoordinator>().BeginCapture();
        _trayIcon.CopyLastColorRequested += (_, _) =>
        {
            var history = _services!.GetRequiredService<IColorHistoryService>();
            if (history.Items.Count > 0)
                _services!.GetRequiredService<IClipboardService>().TrySetText(history.Items[0].Hex);
        };
        _trayIcon.StartWithWindowsToggled += (_, enabled) =>
        {
            _settings.StartWithWindows = enabled;
            _services!.GetRequiredService<IStartupService>().SetEnabled(enabled);
            _services!.GetRequiredService<ISettingsRepository>().Save(_settings);
        };
        _trayIcon.SettingsRequested += (_, _) => OpenSettings();
        _trayIcon.ExitRequested += (_, _) => ExitApplication();

        _trayIcon.SetStartWithWindowsChecked(_services!.GetRequiredService<IStartupService>().IsEnabled);
    }

    private static TrayMenuLabels BuildTrayLabels() => new(
        LocalizationService.Get("L.Tray.Open"),
        LocalizationService.Get("L.Tray.Capture"),
        LocalizationService.Get("L.Tray.CopyLast"),
        LocalizationService.Get("L.Tray.StartWithWindows"),
        LocalizationService.Get("L.Tray.Settings"),
        LocalizationService.Get("L.Tray.Exit"));

    /// <summary>Loads the app's own icon from its embedded resources, falling back to a generic
    /// system icon so the tray never breaks if the asset is ever missing.</summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/tray-icon.ico", UriKind.Absolute);
            var resource = System.Windows.Application.GetResourceStream(uri);
            if (resource is not null)
            {
                // Icon(Stream) reads the data eagerly, so the resource stream can be released
                // right away instead of living for the whole process.
                using var stream = resource.Stream;
                return new System.Drawing.Icon(stream);
            }
        }
        catch
        {
            // fall through to default below
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void SetupHotkey()
    {
        _hotkeyService = _services!.GetRequiredService<IGlobalHotkeyService>();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        _hotkeyService.TryRegister(HotkeyIds.Capture, _settings.Hotkey);
        _hotkeyService.TryRegister(HotkeyIds.OpenApp, _settings.OpenAppHotkey);
        _hotkeyService.TryRegister(HotkeyIds.OpenContrastChecker, _settings.ContrastCheckerHotkey);
        _hotkeyService.TryRegister(HotkeyIds.OpenHarmony, _settings.HarmonyHotkey);
        _hotkeyService.TryRegister(HotkeyIds.OpenPaletteExtractor, _settings.PaletteExtractorHotkey);
    }

    private void OnHotkeyPressed(object? sender, string id)
    {
        switch (id)
        {
            case HotkeyIds.Capture:
                _services!.GetRequiredService<ColorPickerCoordinator>().BeginCapture();
                break;
            case HotkeyIds.OpenApp:
                ToggleMainWindow();
                break;
            case HotkeyIds.OpenContrastChecker:
                OpenContrastChecker();
                break;
            case HotkeyIds.OpenHarmony:
                OpenHarmonyGenerator();
                break;
            case HotkeyIds.OpenPaletteExtractor:
                OpenPaletteExtractor();
                break;
        }
    }

    private void OnSecondInstanceRequested(object? sender, SecondInstanceAction action)
    {
        Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case SecondInstanceAction.StartCapture:
                    _services!.GetRequiredService<ColorPickerCoordinator>().BeginCapture();
                    break;
                default:
                    ShowMainWindow();
                    break;
            }
        });
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>Ctrl+Alt+O acts as a show/hide toggle: if the window is already visible and
    /// active, the same shortcut minimizes it instead of just re-focusing a window the user is
    /// already looking at.</summary>
    private void ToggleMainWindow()
    {
        if (_mainWindow is null) return;

        if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized && _mainWindow.IsActive)
            _mainWindow.WindowState = WindowState.Minimized;
        else
            ShowMainWindow();
    }

    private void OpenSettings()
    {
        var viewModel = new SettingsViewModel(
            _settings,
            _services!.GetRequiredService<ISettingsRepository>(),
            _services!.GetRequiredService<IStartupService>(),
            _hotkeyService!,
            _services!.GetRequiredService<UpdateService>(),
            _services!.GetRequiredService<IThemeService>());

        var window = new SettingsWindow(viewModel) { Owner = _mainWindow };
        window.ShowDialog();
        _trayIcon?.SetStartWithWindowsChecked(_services!.GetRequiredService<IStartupService>().IsEnabled);

        // Reflects a default-copy-format change immediately, without waiting for the next
        // capture/hover to recompute the main window's format readouts.
        var mainViewModel = _services!.GetRequiredService<MainViewModel>();
        mainViewModel.SetCurrent(mainViewModel.CurrentColor);
        mainViewModel.RefreshHotkeyDisplays();
    }

    private void OpenContrastChecker()
    {
        var viewModel = new ContrastCheckerViewModel(_services!.GetRequiredService<ColorPickerCoordinator>(), () => _settings);
        var window = new ContrastCheckerWindow(viewModel) { Owner = _mainWindow };
        window.SettingsRequested += (_, _) =>
        {
            OpenSettings();
            viewModel.RefreshDefaultFormat();
        };
        window.ShowDialog();
    }

    private void OpenHarmonyGenerator()
    {
        var viewModel = new HarmonyViewModel(
            _services!.GetRequiredService<ColorPickerCoordinator>(),
            _services!.GetRequiredService<IClipboardService>(),
            _services!.GetRequiredService<INotificationService>(),
            () => _settings);
        var window = new HarmonyWindow(viewModel) { Owner = _mainWindow };
        window.ShowDialog();
    }

    // Non-modal (unlike the other tool windows): capturing a screen region requires hiding this
    // window and its owner, and hiding a window shown via ShowDialog ends its modal session.
    private PaletteExtractorWindow? _paletteExtractorWindow;

    private void OpenPaletteExtractor()
    {
        if (_paletteExtractorWindow is not null)
        {
            _paletteExtractorWindow.Activate();
            return;
        }

        var viewModel = new PaletteExtractorViewModel(
            _services!.GetRequiredService<IClipboardService>(),
            _services!.GetRequiredService<INotificationService>(),
            () => _settings);
        _paletteExtractorWindow = new PaletteExtractorWindow(viewModel) { Owner = _mainWindow };
        _paletteExtractorWindow.Closed += (_, _) => _paletteExtractorWindow = null;
        _paletteExtractorWindow.Show();
    }

    private void ExitApplication()
    {
        _mainWindow?.AllowExit();
        Shutdown();
    }

    /// <summary>Variante que nunca lança, para código de atualização em threads de fundo.</summary>
    private static void LogErrorSafe(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsFolder);
            AppendErrorLog(exception);
        }
        catch
        {
            // logging must never crash update handling
        }
    }

    private static void AppendErrorLog(Exception exception)
    {
        var logPath = Path.Combine(AppPaths.LogsFolder, "errors.log");
        var entry = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}";
        if (entry.Length > MaxErrorLogEntryCharacters)
            entry = entry[..MaxErrorLogEntryCharacters] + Environment.NewLine;

        var entrySizeBytes = Encoding.UTF8.GetByteCount(entry);
        if (File.Exists(logPath) && new FileInfo(logPath).Length + entrySizeBytes > MaxErrorLogBytes)
            File.Delete(logPath);

        File.AppendAllText(logPath, entry);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstance?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}

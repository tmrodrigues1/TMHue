using System.IO;
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
using TMHue.Windows.Instancing;
using TMHue.Windows.Memory;
using TMHue.Windows.Notifications;
using TMHue.Windows.Persistence;
using TMHue.Windows.Sampling;
using TMHue.Windows.Startup;

namespace TMHue.App;

public partial class App : System.Windows.Application
{
    // Uma exceção isolada da UI é registrada e tolerada; dez em dez segundos indicam uma falha
    // que se repete a cada frame, e continuar só queimaria CPU e disco.
    private readonly FailureBurstDetector _unhandledFailures = new(threshold: 10, TimeSpan.FromSeconds(10));
    private bool _failureShutdownStarted;

    private ServiceProvider? _services;
    private SingleInstanceService? _singleInstance;
    private TrayIconService? _trayIcon;
    private IGlobalHotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private AppSettings _settings = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Start(e);
        }
        catch (Exception exception)
        {
            FailStartup(exception);
        }
    }

    /// <summary>Uma falha no meio da inicialização deixaria o processo vivo sem janela nem ícone
    /// na bandeja, segurando a instância única — o usuário veria apenas "o app não abre". Em vez
    /// disso: registra a causa, avisa onde está o log e encerra.</summary>
    private void FailStartup(Exception exception)
    {
        ErrorReporter.Report("Inicialização; o app não pôde iniciar e foi encerrado", exception);
        ErrorReporter.Flush();
        // MessageBox do WinForms pelo mesmo motivo de ShowMainWindow: não depende do WPF.
        System.Windows.Forms.MessageBox.Show(
            $"O TMHue não pôde iniciar.\n\n{exception.Message}\n\nDetalhes em: {ErrorReporter.LogPath}",
            "TMHue",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);
        Shutdown(1);
    }

    private void Start(StartupEventArgs e)
    {
        // A tray-resident utility must never die silently on a UI exception (e.g. a bad resource
        // while rendering a toast). Log and keep running.
        DispatcherUnhandledException += (_, args) =>
        {
            ErrorReporter.Report("Interface (thread da UI); o app continuou em execução", args.Exception);
            args.Handled = true;
            RegisterUnhandledFailure();
        };

        // O ícone da bandeja é um NotifyIcon do WinForms; exceções disparadas pelos seus eventos
        // de mouse sobem pelo loop de mensagens do WinForms e não passam pelo
        // DispatcherUnhandledException — sem este handler, o usuário vê o diálogo genérico
        // "Microsoft .NET" de erro não tratado.
        System.Windows.Forms.Application.SetUnhandledExceptionMode(
            System.Windows.Forms.UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, args) =>
        {
            ErrorReporter.Report("Ícone da bandeja (WinForms); o app continuou em execução", args.Exception);
            RegisterUnhandledFailure();
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

        Directory.CreateDirectory(AppPaths.RootFolder);
        ErrorReporter.BeginSession();

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

        _services.GetRequiredService<ColorPickerCoordinator>().CaptureFinished += (_, _) => ScheduleMemoryTrim();

        _mainWindow = CreateMainWindow();

        SetupTray();
        SetupHotkey();

        if (!startMinimized)
            ShowMainWindow();
        else
            ScheduleMemoryTrim(); // direto para a bandeja: nada mais vai liberar o pico do startup

        SetupUpdates();
        UpdateUninstallEstimatedSize();
    }

    /// <summary>O Velopack registra a entrada de desinstalação sem o valor "EstimatedSize", e é
    /// exclusivamente desse valor que Configurações > Aplicativos tira o tamanho exibido — sem
    /// ele, o app aparece sem tamanho. Calculado e gravado em segundo plano uma vez por versão
    /// instalada: percorrer a pasta de instalação inteira a cada início seria desperdício, e a
    /// versão muda justamente quando uma atualização altera o tamanho.</summary>
    private void UpdateUninstallEstimatedSize()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        if (version is null || _settings.UninstallSizeVersion == version)
            return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // O exe instalado fica em ...\com.thiagorodrigues.TMHue\current\; a raiz da
                // instalação (com Update.exe) é o pai. Sem Update.exe é um build de
                // desenvolvimento ou portable — nada a fazer.
                var installRoot = Path.GetDirectoryName(
                    AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                if (installRoot is null || !File.Exists(Path.Combine(installRoot, "Update.exe")))
                    return;

                var totalBytes = new DirectoryInfo(installRoot)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);

                using var uninstall = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
                if (uninstall is null) return;

                var rootName = Path.GetFileName(installRoot);
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(name, writable: true);
                    var location = key?.GetValue("InstallLocation") as string
                        ?? key?.GetValue("UninstallString") as string;
                    if (location?.Contains(rootName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // EstimatedSize é em KB, DWORD.
                        key!.SetValue("EstimatedSize", (int)(totalBytes / 1024),
                            Microsoft.Win32.RegistryValueKind.DWord);

                        // AppSettings pertence à thread da UI.
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _settings.UninstallSizeVersion = version;
                            _services?.GetRequiredService<ISettingsRepository>().Save(_settings);
                        }));
                        break;
                    }
                }
            }
            catch
            {
                // cosmético; nunca pode afetar o startup
            }
        });
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
        services.AddSingleton<MemoryTrimmer>();

        services.AddSingleton(sp => new UpdateService(
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<ISettingsRepository>(),
            exception => ErrorReporter.Report("Atualização; a versão atual foi mantida", exception)));

        services.AddSingleton(sp => new ColorPickerCoordinator(
            sp.GetRequiredService<IScreenColorSampler>(),
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IColorHistoryService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<CursorService>(),
            sp.GetRequiredService<IGlobalHotkeyService>(),
            () => sp.GetRequiredService<AppSettings>()));

        services.AddSingleton<MainViewModel>();
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

    /// <summary>WPF proíbe Show() em uma janela já fechada (InvalidOperationException). Se a
    /// MainWindow for realmente fechada por qualquer caminho — "Fechar para a bandeja" desmarcado,
    /// Alt+F4, fim de sessão — uma nova instância é criada aqui, para que o ícone da bandeja, o
    /// atalho global e uma segunda instância sempre consigam reabrir a janela.</summary>
    private MainWindow CreateMainWindow()
    {
        var window = new MainWindow(
            _services!.GetRequiredService<MainViewModel>(),
            _services!.GetRequiredService<ISettingsRepository>(),
            _services!.GetRequiredService<AppSettings>());

        window.SettingsRequested += (_, _) => OpenSettings();
        window.ExitRequested += (_, _) => ExitApplication();
        window.ContrastCheckerRequested += (_, _) => OpenContrastChecker();
        window.HarmonyRequested += (_, _) => OpenHarmonyGenerator();
        window.PaletteExtractorRequested += (_, _) => OpenPaletteExtractor();
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_mainWindow, window))
                _mainWindow = null;
        };
        window.IsVisibleChanged += (_, _) =>
        {
            if (!window.IsVisible) ScheduleMemoryTrim();
        };

        return window;
    }

    private void ShowMainWindow()
    {
        try
        {
            _mainWindow ??= CreateMainWindow();
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
        catch (Exception ex)
        {
            // Se a própria janela não consegue nem renderizar (ex.: DirectWrite com
            // UnauthorizedAccessException por cache de fontes corrompido), avisar via
            // MessageBox do WinForms — que usa GDI, não o pipeline de texto do WPF — em vez
            // de deixar o app "mudo" na bandeja. A janela quebrada é descartada para que a
            // próxima tentativa recrie do zero.
            ErrorReporter.Report("Janela principal; não foi possível exibi-la", ex);
            _mainWindow = null;
            System.Windows.Forms.MessageBox.Show(
                $"Não foi possível abrir a janela do TMHue.\n\n{ex.Message}\n\nDetalhes em: {ErrorReporter.LogPath}",
                "TMHue",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    /// <summary>Ctrl+Alt+O acts as a show/hide toggle: if the window is already visible and
    /// active, the same shortcut minimizes it instead of just re-focusing a window the user is
    /// already looking at.</summary>
    private void ToggleMainWindow()
    {
        if (_mainWindow is not null
            && _mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized && _mainWindow.IsActive)
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
        ScheduleMemoryTrim();
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
        ScheduleMemoryTrim();
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
        ScheduleMemoryTrim();
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
        _paletteExtractorWindow.Closed += (_, _) =>
        {
            _paletteExtractorWindow = null;
            ScheduleMemoryTrim();
        };
        _paletteExtractorWindow.Show();
    }

    private void ExitApplication()
    {
        _mainWindow?.AllowExit();
        Shutdown();
    }

    /// <summary>Devolve memória ao sistema depois que o app sossega: janela enviada à bandeja,
    /// janela de ferramenta fechada ou captura encerrada.</summary>
    private void ScheduleMemoryTrim() => _services?.GetRequiredService<MemoryTrimmer>().ScheduleTrim();

    /// <summary>Disjuntor do handler global. Engolir exceções mantém o app vivo depois de uma
    /// falha isolada, mas uma falha que se repete sem parar não tem como se recuperar sozinha:
    /// avisa o usuário e encerra, em vez de seguir consumindo CPU e disco na bandeja.</summary>
    private void RegisterUnhandledFailure()
    {
        if (_failureShutdownStarted || !_unhandledFailures.RegisterFailure(Environment.TickCount64))
            return;
        _failureShutdownStarted = true;

        ErrorReporter.Note("Disjuntor; o app será encerrado",
            "Dez ou mais exceções não tratadas em dez segundos. As entradas anteriores trazem a causa.");
        ErrorReporter.Flush();
        // MessageBox do WinForms pelo mesmo motivo de ShowMainWindow: não depende do WPF.
        System.Windows.Forms.MessageBox.Show(
            $"O TMHue encontrou erros repetidos e será encerrado.\n\nDetalhes em: {ErrorReporter.LogPath}",
            "TMHue",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);

        try
        {
            ExitApplication();
        }
        catch (Exception exception)
        {
            // A saída limpa passa pela mesma UI que está falhando; se ela também falhar, o
            // processo é encerrado diretamente.
            ErrorReporter.Report("Encerramento após erros repetidos; saída limpa falhou", exception);
            Environment.Exit(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ErrorReporter.Flush();
        _trayIcon?.Dispose();
        _singleInstance?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}

using System.Windows.Forms;

namespace TMHue.Windows.Notifications;

/// <summary>
/// Owns the system tray icon and its context menu via System.Windows.Forms.NotifyIcon
/// (wraps Shell_NotifyIcon). The app stays resident here even when the WPF window is closed.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService(Icon icon)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "TMHue"
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenRequested?.Invoke(this, EventArgs.Empty);
        };

        BuildMenu();
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? CaptureRequested;
    public event EventHandler? CopyLastColorRequested;
    public event EventHandler<bool>? StartWithWindowsToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public NotifyIcon NotifyIcon => _notifyIcon;

    public void SetStartWithWindowsChecked(bool isChecked)
    {
        if (_notifyIcon.ContextMenuStrip?.Items["startWithWindows"] is ToolStripMenuItem item)
            item.Checked = isChecked;
    }

    private void BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var open = new ToolStripMenuItem("Abrir TMHue");
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(open);

        var capture = new ToolStripMenuItem("Capturar cor");
        capture.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(capture);

        var copyLast = new ToolStripMenuItem("Copiar última cor");
        copyLast.Click += (_, _) => CopyLastColorRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(copyLast);

        menu.Items.Add(new ToolStripSeparator());

        var startWithWindows = new ToolStripMenuItem("Iniciar com o Windows") { Name = "startWithWindows", CheckOnClick = true };
        startWithWindows.Click += (_, _) => StartWithWindowsToggled?.Invoke(this, startWithWindows.Checked);
        menu.Items.Add(startWithWindows);

        var settings = new ToolStripMenuItem("Configurações");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settings);

        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Sair");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

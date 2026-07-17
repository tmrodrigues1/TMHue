using System.Windows.Forms;

namespace TMHue.Windows.Notifications;

/// <summary>Localized captions for the tray context menu. The tray lives in the Windows layer
/// (WinForms), which has no access to WPF resource dictionaries, so the app hands the current
/// strings in and re-hands them whenever the UI language changes.</summary>
public sealed record TrayMenuLabels(
    string Open,
    string Capture,
    string CopyLastColor,
    string StartWithWindows,
    string Settings,
    string Exit);

/// <summary>
/// Owns the system tray icon and its context menu via System.Windows.Forms.NotifyIcon
/// (wraps Shell_NotifyIcon). The app stays resident here even when the WPF window is closed.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    private readonly ToolStripMenuItem _open;
    private readonly ToolStripMenuItem _capture;
    private readonly ToolStripMenuItem _copyLast;
    private readonly ToolStripMenuItem _startWithWindows;
    private readonly ToolStripMenuItem _settings;
    private readonly ToolStripMenuItem _exit;

    public TrayIconService(Icon icon, TrayMenuLabels labels)
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

        _open = new ToolStripMenuItem();
        _open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _capture = new ToolStripMenuItem();
        _capture.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);

        _copyLast = new ToolStripMenuItem();
        _copyLast.Click += (_, _) => CopyLastColorRequested?.Invoke(this, EventArgs.Empty);

        _startWithWindows = new ToolStripMenuItem { Name = "startWithWindows", CheckOnClick = true };
        _startWithWindows.Click += (_, _) => StartWithWindowsToggled?.Invoke(this, _startWithWindows.Checked);

        _settings = new ToolStripMenuItem();
        _settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        _exit = new ToolStripMenuItem();
        _exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_open);
        menu.Items.Add(_capture);
        menu.Items.Add(_copyLast);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startWithWindows);
        menu.Items.Add(_settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exit);
        _notifyIcon.ContextMenuStrip = menu;

        SetLabels(labels);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? CaptureRequested;
    public event EventHandler? CopyLastColorRequested;
    public event EventHandler<bool>? StartWithWindowsToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public NotifyIcon NotifyIcon => _notifyIcon;

    public void SetStartWithWindowsChecked(bool isChecked) => _startWithWindows.Checked = isChecked;

    /// <summary>Applies (or re-applies, after a language change) the menu captions.</summary>
    public void SetLabels(TrayMenuLabels labels)
    {
        _open.Text = labels.Open;
        _capture.Text = labels.Capture;
        _copyLast.Text = labels.CopyLastColor;
        _startWithWindows.Text = labels.StartWithWindows;
        _settings.Text = labels.Settings;
        _exit.Text = labels.Exit;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

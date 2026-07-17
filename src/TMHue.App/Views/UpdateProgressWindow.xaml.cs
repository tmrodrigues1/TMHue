using System.Windows;
using TMHue.App.Infrastructure;

namespace TMHue.App.Views;

/// <summary>
/// Modal shown after the user explicitly consents to updating: downloads the release and applies
/// it, with a determinate progress bar. Deliberately non-dismissable while downloading (no close
/// button, Esc ignored, Closing cancelled) — interrupting Velopack mid-apply is the one thing
/// that could leave a broken install.
///
/// Security posture of the whole pipeline (nothing custom, all handled by Velopack):
/// - The feed is the repository's official GitHub Releases over HTTPS (TLS validated by .NET's
///   default certificate chain checks); no access token ships in the client and pre-releases
///   are excluded.
/// - Every downloaded package's SHA checksum is verified against the signed release manifest
///   before it is applied; a corrupted or tampered download fails validation and is discarded,
///   leaving the current version untouched.
/// - Nothing is executed from the download until validation passes, and failures surface here
///   as a simple "try again" without partial state.
/// </summary>
public partial class UpdateProgressWindow : Window
{
    private readonly UpdateService _updateService;
    private bool _finished;

    /// <summary>False when the download/apply failed (in success the app restarts and this
    /// window never returns control to the caller).</summary>
    public bool Succeeded { get; private set; }

    public UpdateProgressWindow(UpdateService updateService)
    {
        InitializeComponent();
        _updateService = updateService;

        // Block every close path (Esc, Alt+F4) until the attempt finishes one way or the other.
        Closing += (_, e) => e.Cancel = !_finished;

        ContentRendered += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var ok = await _updateService.DownloadAndApplyAsync(percent =>
            Dispatcher.Invoke(() => ReportProgress(percent)));

        // Success restarts the process; reaching this line means failure.
        if (!ok)
        {
            Succeeded = false;
            _finished = true;
            Close();
            return;
        }

        // Defensive: if the restart is momentarily delayed, show the final state instead of a
        // frozen bar, and allow closing.
        Succeeded = true;
        _finished = true;
        StatusText.Text = LocalizationService.Get("L.UpdateProgress.Done");
        ReportProgress(100);
    }

    private void ReportProgress(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        PercentText.Text = $"{percent}%";
        ProgressFill.Width = ProgressTrack.ActualWidth * percent / 100.0;

        if (percent >= 100 && !_finished)
            StatusText.Text = LocalizationService.Get("L.UpdateProgress.Installing");
    }
}

using TMHue.App.Views;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Cursor;
using TMHue.Windows.Display;

// Precision keyboard navigation for the eyedropper: 1px per arrow press, Shift+arrow jumps to
// the next detected color change along that axis. Capped by the current monitor's virtual
// bounds so a run of identical pixels near a screen edge can't walk the cursor off-screen.

namespace TMHue.App.Infrastructure;

/// <summary>
/// Drives the full eyedropper cycle: Idle -> Preparing -> Picking -> Captured/Cancelled/Failed -> Idle.
/// Every exit path funnels through <see cref="Cleanup"/> so cursor, windows and sampling state are
/// always released, matching the single-cleanup-routine requirement from the plan.
/// </summary>
public sealed class ColorPickerCoordinator
{
    private readonly IScreenColorSampler _sampler;
    private readonly IClipboardService _clipboard;
    private readonly IColorHistoryService _history;
    private readonly INotificationService _notifications;
    private readonly CursorService _cursorService;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly Func<AppSettings> _settingsAccessor;

    private PickerOverlayWindow? _overlay;
    private MagnifierWindow? _magnifier;
    private RgbColor? _lastColor;
    private CopyFormat _activeFormat;
    private bool _cleaningUp;

    // Set right before a keyboard-driven SetCursorPos so the WM_MOUSEMOVE it generates samples a
    // single exact pixel instead of running the user's SampleAreaSize average: averaging over an
    // NxN region (CopyFromScreen + median sort) is what actually froze/stuttered the UI when it
    // ran on every step of a keyboard scan, and precision pixel navigation should read the exact
    // pixel under the cursor anyway rather than a smoothed neighborhood.
    private bool _forceSinglePixelSample;

    public ColorPickerCoordinator(
        IScreenColorSampler sampler,
        IClipboardService clipboard,
        IColorHistoryService history,
        INotificationService notifications,
        CursorService cursorService,
        IGlobalHotkeyService hotkeys,
        Func<AppSettings> settingsAccessor)
    {
        _sampler = sampler;
        _clipboard = clipboard;
        _history = history;
        _notifications = notifications;
        _cursorService = cursorService;
        _hotkeys = hotkeys;
        _settingsAccessor = settingsAccessor;

        _hotkeys.HotkeyPressed += (_, id) =>
        {
            if (id == HotkeyIds.CancelCapture && State == PickerState.Picking)
                Finish(PickerState.Cancelled);
        };
    }

    public PickerState State { get; private set; } = PickerState.Idle;

    public event EventHandler<CapturedColor>? Captured;

    /// <summary>Raised live while picking, for the main window to mirror the hovered color.
    /// No floating tooltip follows the cursor; only the brush cursor is shown on screen.</summary>
    public event EventHandler<RgbColor>? HoverColorChanged;

    /// <summary>Raised when 1/2/3 switches the format for the in-progress capture, so the main
    /// window's own readout (still showing the last hovered color while picking) can switch to
    /// match immediately instead of waiting for the next mouse move or the capture to finish.</summary>
    public event EventHandler<CopyFormat>? ActiveFormatChanged;

    /// <summary>Cancels an in-progress capture, releasing the overlay/magnifier/hotkey exactly
    /// like an Esc press would. No-op if not currently picking. Lets a caller that started a
    /// capture (e.g. the contrast checker window) clean up after itself if it's closed or
    /// disposed mid-capture, instead of leaving the eyedropper overlay orphaned on screen and
    /// <see cref="State"/> stuck at <see cref="PickerState.Picking"/> forever.</summary>
    public void CancelCapture()
    {
        if (State == PickerState.Picking)
            Finish(PickerState.Cancelled);
    }

    public void BeginCapture()
    {
        if (State != PickerState.Idle) return;

        State = PickerState.Preparing;

        _activeFormat = _settingsAccessor().CopyFormat;

        var eyedropperCursor = _cursorService.GetEyedropperCursor();

        _overlay = new PickerOverlayWindow { Cursor = eyedropperCursor };

        _overlay.PointerMoved += OnPointerMoved;
        _overlay.PointerClicked += OnPointerClicked;
        _overlay.CancelRequested += (_, _) => Finish(PickerState.Cancelled);
        _overlay.FormatKeyPressed += OnFormatKeyPressed;
        _overlay.ArrowKeyPressed += OnArrowKeyPressed;

        _overlay.Show();
        _overlay.Activate();

        // WPF's per-window Cursor property is unreliable on transparent/layered (AllowsTransparency)
        // windows: WM_SETCURSOR often never reaches them. Mouse.OverrideCursor forces it app-wide
        // for the duration of the capture and is reset unconditionally in Cleanup().
        System.Windows.Input.Mouse.OverrideCursor = eyedropperCursor;

        if (_settingsAccessor().ShowMagnifier)
        {
            _magnifier = new MagnifierWindow();
            _magnifier.Show();
        }

        // System-wide Esc for the duration of the capture: focus-based Esc handling on the
        // overlay can miss if activation is stolen, and a stuck eyedropper has no other way out.
        _hotkeys.TryRegister(HotkeyIds.CancelCapture,
            new HotkeyDefinition { Modifiers = new List<string>(), Key = "ESCAPE" });

        State = PickerState.Picking;
    }

    // Latest unprocessed pointer position. High-polling mice deliver WM_MOUSEMOVEs far faster
    // than the screen refreshes; sampling every one of them wastes a BitBlt + median per event.
    // OnPointerMoved only records the newest position and schedules a single dispatcher callback,
    // so a burst of moves collapses into one sample at the final position — never a stale one.
    private (int X, int Y)? _pendingMove;

    private void OnPointerMoved(object? sender, (int PhysicalX, int PhysicalY) pos)
    {
        if (State != PickerState.Picking) return;

        // Keyboard nudges bypass coalescing: the single-pixel flag must be consumed by exactly
        // this position, not whatever position happens to be pending when the callback runs.
        if (_forceSinglePixelSample)
        {
            _pendingMove = null;
            ProcessPointerMove(pos.PhysicalX, pos.PhysicalY);
            return;
        }

        var alreadyScheduled = _pendingMove is not null;
        _pendingMove = (pos.PhysicalX, pos.PhysicalY);
        if (!alreadyScheduled)
        {
            // Render, not Input: Input sits below Render in dispatcher priority, so queued
            // render work (composition, layout) would run first and delay this by up to a
            // whole extra cycle — visible as the magnifier trailing the cursor. Render priority
            // runs this right before the next composition pass, giving exactly one sample per
            // rendered frame with no added lag.
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Render, ProcessPendingMove);
        }
    }

    private void ProcessPendingMove()
    {
        if (_pendingMove is not { } pos) return;
        _pendingMove = null;

        if (State != PickerState.Picking) return;
        ProcessPointerMove(pos.X, pos.Y);
    }

    private void ProcessPointerMove(int physicalX, int physicalY)
    {
        try
        {
            var settings = _settingsAccessor();

            var sampleSize = _forceSinglePixelSample ? 1 : settings.SampleAreaSize;
            _forceSinglePixelSample = false;

            // One screen capture serves both the sampling median and the magnifier — capturing
            // a region sized for whichever is larger, instead of one BitBlt each.
            RgbColor[,]? region = null;
            var regionSide = 0;
            if (sampleSize > 1 || _magnifier is not null)
            {
                regionSide = Math.Max(sampleSize, _magnifier is not null ? MagnifierWindow.RegionSide : 1);
                region = _sampler.ReadRegion(physicalX, physicalY, regionSide);
            }

            RgbColor color;
            if (sampleSize > 1)
            {
                color = RgbColor.MedianOf(region!, regionSide, sampleSize);
            }
            else if (!_sampler.TryReadPixel(physicalX, physicalY, out color))
            {
                return;
            }

            _lastColor = color;
            HoverColorChanged?.Invoke(this, color);

            if (_magnifier is not null)
            {
                _magnifier.UpdateContent(region!, regionSide, color, _activeFormat);
                _magnifier.MoveTo(physicalX, physicalY);
            }
        }
        catch
        {
            Finish(PickerState.Failed);
        }
    }

    /// <summary>Lets 1/2/3/4 switch the format used for this capture only, without touching the
    /// user's persisted default in <see cref="AppSettings.CopyFormat"/>.</summary>
    private void OnFormatKeyPressed(object? sender, CopyFormat format)
    {
        if (State != PickerState.Picking) return;

        _activeFormat = format;

        if (_magnifier is not null && _lastColor is { } color)
            _magnifier.UpdateFormat(color, _activeFormat);

        ActiveFormatChanged?.Invoke(this, format);
    }

    /// <summary>Moves the system cursor with the keyboard while picking. Plain arrows nudge by a
    /// single physical pixel; Shift+arrow scans along that axis for the next pixel whose color
    /// differs from the one currently under the cursor, so lining up on a color boundary doesn't
    /// require dozens of individual presses. WM_MOUSEMOVE fires from the OS as usual after
    /// SetCursorPos, so OnPointerMoved picks up the new position without any extra plumbing.</summary>
    private void OnArrowKeyPressed(object? sender, (int Dx, int Dy, bool JumpToColorChange) e)
    {
        if (State != PickerState.Picking) return;

        var (x, y) = OverlayPlacement.GetPhysicalCursorPosition();
        var bounds = TMHue.Windows.Display.VirtualScreenBounds.GetCurrent();

        int targetX, targetY;

        if (e.JumpToColorChange)
        {
            // Capped well below screen size, and small enough that scanning it (one GetPixel
            // syscall per step) doesn't feel laggy even when no color change is found — e.g. a
            // large solid-color area, where the scan runs to the cap on every single press.
            const int maxSteps = 80;

            var stepX = Math.Clamp(x + e.Dx * maxSteps, bounds.Left, bounds.Right - 1) - x;
            var stepY = Math.Clamp(y + e.Dy * maxSteps, bounds.Top, bounds.Bottom - 1) - y;
            var steps = Math.Max(Math.Abs(stepX), Math.Abs(stepY));
            if (steps <= 0) return;

            // No color change within range (e.g. a solid-color area): rather than leaving the
            // cursor stuck in place, move it the full scanned distance so holding/repeating the
            // key still feels like free, continuous movement instead of a dead key press.
            if (!_sampler.TryFindNextColorChange(x, y, e.Dx, e.Dy, steps, out var found))
                found = (x + stepX, y + stepY);

            targetX = found.X;
            targetY = found.Y;
        }
        else
        {
            targetX = Math.Clamp(x + e.Dx, bounds.Left, bounds.Right - 1);
            targetY = Math.Clamp(y + e.Dy, bounds.Top, bounds.Bottom - 1);
        }

        _forceSinglePixelSample = true;
        OverlayPlacement.SetCursorPosition(targetX, targetY);
    }

    private void OnPointerClicked(object? sender, (int PhysicalX, int PhysicalY) pos)
    {
        if (State != PickerState.Picking || _lastColor is not { } color) return;

        var settings = _settingsAccessor();
        var captured = CapturedColor.FromRgb(color, uppercaseHex: true);
        var copiedText = color.Format(_activeFormat);

        var copied = _clipboard.TrySetText(copiedText);
        _history.Add(captured);

        Finish(PickerState.Captured);

        if (copied && settings.ShowCaptureNotification)
            _notifications.ShowCaptureConfirmation(captured.Hex, copiedText);

        Captured?.Invoke(this, captured);
    }

    private void Finish(PickerState finalState)
    {
        State = finalState;
        Cleanup();
        State = PickerState.Idle;
    }

    private void Cleanup()
    {
        // Closing the overlay fires its Deactivated event, whose Cancel handler re-enters
        // Finish/Cleanup while the window is still closing — Close() would then throw and abort
        // the caller (swallowing the capture toast). The guard makes re-entry a no-op.
        if (_cleaningUp) return;
        _cleaningUp = true;

        try
        {
            _hotkeys.Unregister(HotkeyIds.CancelCapture);
            System.Windows.Input.Mouse.OverrideCursor = null;

            if (_overlay is not null)
            {
                _overlay.PointerMoved -= OnPointerMoved;
                _overlay.PointerClicked -= OnPointerClicked;
                _overlay.FormatKeyPressed -= OnFormatKeyPressed;
                _overlay.ArrowKeyPressed -= OnArrowKeyPressed;
                _overlay.Close();
                _overlay = null;
            }

            if (_magnifier is not null)
            {
                _magnifier.Close();
                _magnifier = null;
            }

            _lastColor = null;
            _pendingMove = null;
        }
        finally
        {
            _cleaningUp = false;
        }
    }
}

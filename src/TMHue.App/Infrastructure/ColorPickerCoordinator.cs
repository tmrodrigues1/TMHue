using TMHue.App.Views;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Cursor;
using TMHue.Windows.Display;

namespace TMHue.App.Infrastructure;

/// <summary>Owns the UI-thread selection lifecycle. Every exit releases windows, pending
/// dispatcher work and the temporary cancellation hotkey.</summary>
public sealed partial class ColorPickerCoordinator
{
    // Esc is a system-wide hotkey while a capture is being prepared; a stuck preparation must
    // not keep it captured.
    private static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(5);

    private readonly IScreenColorSampler _sampler;
    private readonly IClipboardService _clipboard;
    private readonly IColorHistoryService _history;
    private readonly INotificationService _notifications;
    private readonly CursorService _cursorService;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly Func<AppSettings> _settingsAccessor;
    private PickerOverlayHost? _overlays;
    private PickerCaptureResources? _resources;
    private MonitorLayout? _layout;
    private PickerRefreshLoop? _refresh;
    private int _generation;
    private CancellationTokenSource? _preparationCancellation;
    private int _colorTolerance;
    private RgbColor? _publishedColor;
    private MagnifierWindow? _magnifier;
    private ColorSelectionSession? _session;
    private CopyFormat _activeFormat;
    private bool _cleaningUp;

    public ColorPickerCoordinator(IScreenColorSampler sampler, IClipboardService clipboard,
        IColorHistoryService history, INotificationService notifications, CursorService cursorService,
        IGlobalHotkeyService hotkeys, Func<AppSettings> settingsAccessor)
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
            if (id == HotkeyIds.CancelCapture) CancelCapture();
        };
    }

    public PickerState State { get; private set; } = PickerState.Idle;
    public event EventHandler<CapturedColor>? Captured;
    public event EventHandler<RgbColor>? HoverColorChanged;
    public event EventHandler<CopyFormat>? ActiveFormatChanged;

    /// <summary>Raised after every capture exit (captured, cancelled or failed), once all
    /// session resources have been released.</summary>
    public event EventHandler? CaptureFinished;

    public void CancelCapture()
    {
        if (State is PickerState.Picking or PickerState.Preparing) Finish(PickerState.Cancelled);
    }

    public async void BeginCapture()
    {
        if (State != PickerState.Idle) return;
        State = PickerState.Preparing;
        var generation = ++_generation;
        PickerCaptureResources? prepared = null;
        try
        {
            var cancellation = new CancellationTokenSource();
            _preparationCancellation = cancellation;
            var token = cancellation.Token;
            var settings = _settingsAccessor();
            _activeFormat = settings.CopyFormat;
            var sampleSide = settings.SampleAreaSize;
            var showMagnifier = settings.ShowMagnifier;
            var freeze = settings.FreezeScreenWhilePicking;
            var precision = settings.UsePrecisionCursor;
            var output = settings.ColorOutput;
            _colorTolerance = Math.Clamp(settings.ColorChangeTolerance, 0, 32);
            var layout = new MonitorLayout();
            _hotkeys.TryRegister(HotkeyIds.CancelCapture,
                new HotkeyDefinition { Modifiers = new List<string>(), Key = "ESCAPE" });
            cancellation.CancelAfter(PreparationTimeout);
            var preparation = Task.Run(() => PickerCaptureResources.Prepare(_sampler, layout, output, freeze, token), token);
            try
            {
                // WaitAsync returns on cancellation/timeout even if the worker itself is stuck.
                prepared = await preparation.WaitAsync(token);
            }
            catch
            {
                DisposeWhenCompleted(preparation);
                throw;
            }
            if (generation != _generation || State != PickerState.Preparing)
            { prepared.Dispose(); return; }
            _resources = prepared;
            cancellation.Dispose(); _preparationCancellation = null;
            _layout = layout;
            if (prepared.ErrorResource is { } error)
            { FailCapture(error); return; }
            if (prepared.Sampler is null) { FailCapture("L.Capture.ReadFailed"); return; }
            _session = new ColorSelectionSession(prepared.Sampler, sampleSide);
            var cursor = precision ? _cursorService.GetPrecisionCursor() : _cursorService.GetEyedropperCursor();
            _overlays = new(layout, prepared.Snapshot, cursor);
            foreach (var window in _overlays.Windows) SubscribeOverlay(window, true);
            var (x, y) = OverlayPlacement.GetPhysicalCursorPosition();
            if (!_overlays.Show(x, y, layout))
            {
                FailCapture("L.Capture.ExclusionFailed");
                return;
            }
            System.Windows.Input.Mouse.OverrideCursor = cursor;

            if (showMagnifier)
            {
                _magnifier = new MagnifierWindow(layout);
                _magnifier.Show();
                if (!_magnifier.CaptureExclusionStatus.Succeeded)
                {
                    FailCapture("L.Capture.ExclusionFailed");
                    return;
                }
            }
            State = PickerState.Picking;
            _refresh = new(freeze, x, y, ProcessPointerMove);
            _refresh.ReadNow(x, y);
        }
        catch
        {
            if (generation == _generation) FailCapture("L.Capture.ReadFailed");
            else prepared?.Dispose();
        }
    }

    private void OnCancelRequested(object? sender, EventArgs e) => CancelCapture();
    private void OnReadFailed(object? sender, EventArgs e) => FailCapture("L.Capture.ReadFailed");

    private void OnPointerMoved(object? sender, (int PhysicalX, int PhysicalY) pos)
    {
        if (State != PickerState.Picking) return;
        if (_layout?.At(pos.PhysicalX, pos.PhysicalY) is null) return;
        _refresh?.Move(pos.PhysicalX, pos.PhysicalY);
    }

    private void ProcessPointerMove(int x, int y)
    {
        if (State != PickerState.Picking) return;
        try
        {
            var displaySide = _magnifier is null ? 1 : MagnifierWindow.RegionSide;
            if (_session is null || !_session.TryPreview(x, y, displaySide, out var sample)
                || _session.Latest is not { } color)
            {
                FailRead();
                return;
            }
            if (_publishedColor != color)
            {
                _publishedColor = color;
                HoverColorChanged?.Invoke(this, color);
            }
            if (_magnifier is null) return;
            _magnifier.UpdateContent(sample!, color, _activeFormat, _session.SampleSide);
            _magnifier.MoveTo(x, y);
        }
        catch
        {
            FailCapture("L.Capture.ReadFailed");
        }
    }

    private void OnFormatKeyPressed(object? sender, CopyFormat format)
    {
        if (State != PickerState.Picking) return;
        _activeFormat = format;
        if (_session?.Latest is { } color) _magnifier?.UpdateFormat(color, format);
        ActiveFormatChanged?.Invoke(this, format);
    }

    private void OnPointerClicked(object? sender, (int PhysicalX, int PhysicalY) pos)
    {
        if (State != PickerState.Picking) return;
        _refresh?.Dispose(); _refresh = null;
        RgbColor color;
        try
        {
            if (_session is null || !_session.TryConfirm(pos.PhysicalX, pos.PhysicalY, out color))
            {
                FailRead();
                return;
            }
        }
        catch
        {
            FailCapture("L.Capture.ReadFailed");
            return;
        }
        var settings = _settingsAccessor();
        var captured = CapturedColor.FromRgb(color, uppercaseHex: true) with { CaptureSource = _session?.SourceDescription };
        var text = color.Format(_activeFormat);
        // Close before clipboard retries can pump messages. Color is a local immutable value.
        Finish(PickerState.Captured);
        var copied = _clipboard.TrySetText(text);
        _history.Add(captured);
        if (copied && settings.ShowCaptureNotification)
            _notifications.ShowCaptureConfirmation(captured.Hex, text);
        Captured?.Invoke(this, captured);
    }

    private void FailCapture(string resource)
    {
        if (_cleaningUp || State == PickerState.Idle) return;
        Finish(PickerState.Failed);
        _notifications.ShowCaptureFailure(LocalizationService.Get(resource));
    }

    private void FailRead() => FailCapture(_resources?.Sampler?.ColorManagementFailed is true
        ? "L.Capture.ColorConversionFailed" : "L.Capture.ReadFailed");

    private void Finish(PickerState finalState)
    {
        if (_cleaningUp) return;
        State = finalState;
        Cleanup();
        State = PickerState.Idle;
        CaptureFinished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>An abandoned preparation may still finish later; its snapshot must not outlive it.</summary>
    private static void DisposeWhenCompleted(Task<PickerCaptureResources> preparation) =>
        preparation.ContinueWith(static completed => completed.Result.Dispose(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private void Cleanup()
    {
        if (_cleaningUp) return;
        _cleaningUp = true;
        try
        {
            _generation++;
            _preparationCancellation?.Cancel();
            _preparationCancellation?.Dispose(); _preparationCancellation = null;
            _refresh?.Dispose(); _refresh = null;
            _session = null;
            _publishedColor = null;
            _hotkeys.Unregister(HotkeyIds.CancelCapture);
            System.Windows.Input.Mouse.OverrideCursor = null;
            if (_overlays is not null)
            {
                foreach (var window in _overlays.Windows) SubscribeOverlay(window, false);
                _overlays.Dispose(); _overlays = null;
            }
            _magnifier?.Close();
            _magnifier = null;
            _resources?.Dispose(); _resources = null; _layout = null;
        }
        finally { _cleaningUp = false; }
    }

    private void SubscribeOverlay(PickerOverlayWindow window, bool add)
    {
        if (add)
        {
            window.PointerMoved += OnPointerMoved; window.PointerClicked += OnPointerClicked;
            window.CancelRequested += OnCancelRequested; window.ReadFailed += OnReadFailed;
            window.FormatKeyPressed += OnFormatKeyPressed; window.ArrowKeyPressed += OnArrowKeyPressed;
        }
        else
        {
            window.PointerMoved -= OnPointerMoved; window.PointerClicked -= OnPointerClicked;
            window.CancelRequested -= OnCancelRequested; window.ReadFailed -= OnReadFailed;
            window.FormatKeyPressed -= OnFormatKeyPressed; window.ArrowKeyPressed -= OnArrowKeyPressed;
        }
    }
}

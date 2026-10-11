using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TMHue.App.Infrastructure;
using TMHue.App.Views;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Cursor;
using TMHue.Windows.Display;

namespace SamplingDiagnostics;

internal static class CoordinatorValidation
{
    public static async Task<object> Run()
    {
        await ValidateContinuousMovement();
        var sampler = new FakeSampler();
        var effects = new FakeEffects();
        var settings = new AppSettings { ShowMagnifier = false, ShowCaptureNotification = false };
        var coordinator = new ColorPickerCoordinator(sampler, effects, effects, effects,
            new CursorService("TMHue;component/Assets/eyedropper.cur"), effects, () => settings);
        try
        {
            coordinator.BeginCapture();
            coordinator.CancelCapture(); // cancel while the worker is still preparing
            Require(coordinator.State == PickerState.Idle, "preparation cancellation");
            coordinator.BeginCapture(); // old completion must not discard this generation
            await Until(() => coordinator.State == PickerState.Picking && sampler.Reads > 0);
            await Task.Delay(100);
            Require(coordinator.State == PickerState.Picking, "stale preparation cannot close new session");
            var overlay = System.Windows.Application.Current.Windows.OfType<PickerOverlayWindow>().First();
            var hwnd = new WindowInteropHelper(overlay).Handle;
            var packed = (nint)((uint)5 | ((uint)5 << 16));
            Require(MouseMessagePosition.TryGetScreenPosition(hwnd, packed, out var expected), "click position");
            SendMessage(hwnd, 0x0201, 1, packed); // own test window only; no system mouse movement
            Require(coordinator.State == PickerState.Idle && effects.Items.Count == 1 && effects.Copies == 1,
                "one confirmation and complete cleanup");
            Require(sampler.LastPosition == expected && effects.Items[0].ToRgb() == FakeSampler.ColorAt(expected.X, expected.Y),
                "native click rereads actual position");
            var reads = sampler.Reads;
            await Task.Delay(150);
            Require(sampler.Reads == reads, "no pending preview after click");

            sampler.Fail = true;
            coordinator.BeginCapture();
            await Until(() => effects.Failures == 1);
            Require(coordinator.State == PickerState.Idle && effects.Items.Count == 1 && effects.Copies == 1,
                "failed initial read cannot save old color");

            sampler.Fail = false;
            coordinator.BeginCapture();
            await Until(() => coordinator.State == PickerState.Picking);
            effects.Cancel();
            Require(coordinator.State == PickerState.Idle && effects.Registered == 0, "Escape releases registration");
            Require(!System.Windows.Application.Current.Windows.OfType<PickerOverlayWindow>().Any(), "no stranded overlays");
            return new { Passed = true, PreparationCancellationPassed = true, NativeClickPassed = true,
                PendingPreviewStopped = true, FailedReadDiscarded = true, EscapeCleanupPassed = true,
                ContinuousMovementPassed = true };
        }
        finally { coordinator.CancelCapture(); }
    }

    private static async Task ValidateContinuousMovement()
    {
        foreach (var frozen in new[] { false, true })
        {
            var positions = new List<(int X, int Y)>();
            using var refresh = new PickerRefreshLoop(frozen, 0, 0, (x, y) => positions.Add((x, y)));
            refresh.ReadNow(0, 0);
            // Mouse events arrive faster than the sampling interval. The lens must
            // continue receiving frames before the movement stream ends.
            for (var x = 1; x <= 60; x++)
            {
                refresh.Move(x, -x);
                await Task.Delay(5);
            }
            Require(positions.Count >= 3 && positions.Any(p => p.X is > 0 and < 60 && p.Y == -p.X),
                $"continuous movement refresh (frozen={frozen})");
            await Task.Delay(120);
            Require(positions[^1] == (60, -60), "last pointer position is sampled");
            refresh.Dispose();
            var reads = positions.Count;
            await Task.Delay(120);
            Require(positions.Count == reads, "movement refresh stops on disposal");
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= end) throw new InvalidOperationException("Coordinator validation timed out");
            await Task.Delay(20);
        }
    }

    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException($"Coordinator validation failed: {name}"); }

    private sealed class FakeSampler : IScreenColorSampler
    {
        public bool Fail { get; set; }
        public int Reads { get; private set; }
        public (int X, int Y) LastPosition { get; private set; }
        public static RgbColor ColorAt(int x, int y) => new((byte)(x & 255), (byte)(y & 255), 224);
        public bool TryReadPixel(int x, int y, out RgbColor color) { color = ColorAt(x, y); return !Fail; }
        public bool TryReadRegion(int x, int y, int side, out ScreenSample? sample)
        {
            Reads++; LastPosition = (x, y); sample = null;
            if (Fail) return false;
            sample = new(side);
            for (var row = 0; row < side; row++)
            for (var col = 0; col < side; col++)
            { sample.Pixels[row, col] = ColorAt(x + col - side / 2, y + row - side / 2); sample.Valid[row, col] = true; }
            return true;
        }
        public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, out (int X, int Y) result)
        { result = default; return false; }
    }

    private sealed class FakeEffects : IClipboardService, IColorHistoryService, INotificationService, IGlobalHotkeyService
    {
        private readonly List<CapturedColor> _items = new();
        public IReadOnlyList<CapturedColor> Items => _items;
        public int Copies { get; private set; }
        public int Failures { get; private set; }
        public int Registered { get; private set; }
        public event EventHandler<string>? HotkeyPressed;
        public event EventHandler? Changed { add { } remove { } }
        public bool TrySetText(string text, int maxAttempts = 3) { Copies++; return true; }
        public void Add(CapturedColor color) => _items.Add(color);
        public void Clear() => _items.Clear();
        public void TogglePin(CapturedColor color) { }
        public void Load() { }
        public void Save() { }
        public void ShowCaptureConfirmation(string hex, string? displayText = null) { }
        public void ShowCaptureFailure(string message) => Failures++;
        public bool TryRegister(string id, HotkeyDefinition hotkey) { Registered = 1; return true; }
        public void Unregister(string id) => Registered = 0;
        public void UnregisterAll() => Registered = 0;
        public void Dispose() { }
        public void Cancel() => HotkeyPressed?.Invoke(this, HotkeyIds.CancelCapture);
    }

    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}

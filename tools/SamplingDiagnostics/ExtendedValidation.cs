using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.App.Infrastructure;
using TMHue.App.Views;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Cursor;
using TMHue.Windows.Display;
using TMHue.Windows.Sampling;

namespace SamplingDiagnostics;

internal static class ExtendedValidation
{
    public static async Task<object> Run(Window target, MagnifierWindow lens, ScreenColorSampler live)
    {
        var layout = new MonitorLayout();
        var environment = await Task.Run(() => DisplayColorEnvironment.Read(layout));
        var dpi = VisualTreeHelper.GetDpi(target);
        var point = target.PointToScreen(new Point(100 / dpi.DpiScaleX, 40 / dpi.DpiScaleY));
        var x = (int)point.X; var y = (int)point.Y;
        var blue = new RgbColor(58, 16, 224);
        Require(live.TryReadPixel(x, y, out var initial) && initial == blue, "initial known pixel");
        var measurements = Benchmark(live, x, y);
        var gradient = target.PointToScreen(new Point(100 / dpi.DpiScaleX, 180 / dpi.DpiScaleY));
        Require(live.TryFindNextColorChange((int)gradient.X, (int)gradient.Y, 1, 0, 80, 2, out var transition)
            && transition == ((int)gradient.X + 3, (int)gradient.Y), "tolerant strip navigation");
        Require(live.TryFindNextColorChange((int)gradient.X, (int)gradient.Y, 1, 0, 80, 0, out transition)
            && transition == ((int)gradient.X + 1, (int)gradient.Y), "exact strip navigation");

        using var host = new PickerOverlayHost(layout, null, System.Windows.Input.Cursors.Cross);
        Require(host.Show(x, y, layout), "per-monitor exclusion");
        await Task.Delay(100);
        var windows = host.Windows.Select((window, i) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            Require(GetWindowRect(hwnd, out var actual), "window bounds query");
            var expected = layout.Monitors[i].Bounds;
            Require(actual.Left == expected.Left && actual.Top == expected.Top
                && actual.Right == expected.Right && actual.Bottom == expected.Bottom, "physical monitor coverage");
            var packed = (nint)((uint)5 | ((uint)5 << 16));
            Require(MouseMessagePosition.TryGetScreenPosition(hwnd, packed, out var click)
                && click == (expected.Left + 5, expected.Top + 5), "native click coordinates");
            var inside = new NativePoint { X = expected.Left + 10, Y = expected.Top + 10 };
            Require(WindowFromPoint(inside) == hwnd, "overlay receives input within monitor");
            lens.MoveTo(expected.Right - 1, expected.Bottom - 1);
            var lensHwnd = new System.Windows.Interop.WindowInteropHelper(lens).Handle;
            Require(GetWindowRect(lensHwnd, out var lensBounds)
                && lensBounds.Left >= expected.Left && lensBounds.Top >= expected.Top
                && lensBounds.Right <= expected.Right && lensBounds.Bottom <= expected.Bottom,
                "lens remains inside current monitor after transition");
            return new { layout.Monitors[i].DeviceName, Bounds = expected,
                Scale = OverlayPlacement.GetWindowScale(hwnd), ClickCoordinatesPassed = true,
                HitTestPassed = true, LensBoundsPassed = true };
        }).ToArray();
        Require(live.TryReadPixel(x, y, out var excluded) && excluded == blue, "blue beneath monitor overlays");

        Require(FrozenScreenSampler.TryCreate(layout, out var frozen), "snapshot creation");
        using var snapshot = frozen;
        var image = (Image)target.Content;
        var original = image.Source;
        var selected = new List<RgbColor>();
        var updateTimes = new List<double>();
        var session = new ColorSelectionSession(live, 1);
        var firstWrite = lens.BitmapUpdateCount;
        using (var refresh = new PickerRefreshLoop(false, x, y, (px, py) =>
        {
            var started = Stopwatch.GetTimestamp();
            Require(session.TryPreview(px, py, 11, out var sample), "live frame");
            var color = session.Latest ?? throw new InvalidOperationException("missing color");
            lens.UpdateContent(sample!, color, CopyFormat.Hex, 1);
            Require(lens.DisplayedValue == color.ToHex(), "lens and readout agreement");
            selected.Add(color);
            updateTimes.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }))
        {
            refresh.ReadNow(x, y);
            await Task.Delay(100);
            var stableWrites = lens.BitmapUpdateCount;
            Require(stableWrites <= firstWrite + 1, "no redundant bitmap writes");
            var pixels = new byte[320 * 220 * 4];
            for (var index = 0; index < pixels.Length; index += 4) pixels[index + 1] = 255;
            image.Source = BitmapSource.Create(320, 220, 96, 96, PixelFormats.Bgr32, null, pixels, 320 * 4);
            await Task.Delay(150);
            Require(selected.Contains(new RgbColor(0, 255, 0)), "stationary animated content refresh");
            Require(snapshot!.TryReadPixel(x, y, out var fixedColor) && fixedColor == blue, "snapshot remains exact");
            await Task.Delay(600); // collect warmed-up stationary update timings
            refresh.Dispose();
            var reads = selected.Count;
            await Task.Delay(80);
            Require(selected.Count == reads, "refresh stops on disposal");
        }
        image.Source = original;
        await Task.Delay(50);

        using var frozenHost = new PickerOverlayHost(layout, snapshot, System.Windows.Input.Cursors.Cross);
        Require(frozenHost.Show(x, y, layout), "frozen overlay exclusion");
        await Task.Delay(60);
        var frozenSession = new ColorSelectionSession(snapshot!, 1);
        Require(frozenSession.TryConfirm(x, y, out var confirmed) && confirmed == blue, "frozen click confirmation");
        using var source = new ColorAwareSampler(live, CaptureColorOutput.Rendered, layout, environment, null);
        Require(source.TryReadPixel(x, y, out var raw) && raw == blue, "default output remains unchanged");
        var srgbPath = DisplayColorEnvironment.GetSrgbProfilePath();
        using var srgb = new ColorAwareSampler(live, CaptureColorOutput.Srgb, layout, environment, srgbPath);
        var srgbVerified = srgb.TryReadPixel(x, y, out var converted);
        if (environment.All(e => e.AdvancedColorActive is false && e.ProfilePath == srgbPath))
            Require(srgbVerified && converted == blue, "sRGB identity on configured monitors");
        var profileComparison = ProfileValidation.Compare(srgbPath);
        var service = new CursorService("TMHue;component/Assets/eyedropper.cur");
        Require(service.GetPrecisionCursor() != System.Windows.Input.Cursors.None, "precision cursor creation");
        var warmedUp = updateTimes.Skip(1).Order().ToArray();
        return new { Passed = true, Environment = environment, Monitors = windows, Latency = measurements,
            StationaryRefreshPassed = true, StableFrameRenderingPassed = true, FrozenConfirmationPassed = true,
            TolerantNavigationPassed = true, SrgbConversionAvailable = srgbVerified,
            SrgbValue = srgbVerified ? converted.ToHex() : null, ProfileComparison = profileComparison,
            LiveUpdate = new { Reads = updateTimes.Count, InitialMs = updateTimes[0],
                WarmMedianMs = warmedUp[warmedUp.Length / 2], WarmP95Ms = warmedUp[(warmedUp.Length - 1) * 95 / 100],
                Scope = "capture, selection and WPF bitmap/readout update; excludes compositor presentation" } };
    }

    private static object[] Benchmark(ScreenColorSampler sampler, int x, int y)
    {
        return new[] { 1, 5, 11 }.Select(side =>
        {
            var elapsed = new double[100];
            for (var index = 0; index < elapsed.Length; index++)
            {
                var start = Stopwatch.GetTimestamp();
                Require(sampler.TryReadRegion(x, y, side, out var sample) && sample!.TrySelectColor(side, out _), "benchmark read");
                elapsed[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            Array.Sort(elapsed);
            return (object)new { Side = side, Reads = elapsed.Length, MedianMs = elapsed[50], P95Ms = elapsed[95], MaxMs = elapsed[^1] };
        }).ToArray();
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException($"Validation failed: {check}");
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
}

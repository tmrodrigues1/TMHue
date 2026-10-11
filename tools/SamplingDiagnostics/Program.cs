using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.App.Views;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;
using TMHue.Windows.Sampling;

namespace SamplingDiagnostics;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var output = args.FirstOrDefault() ?? "sampling-results.json";
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var target = CreateTarget();
        target.Show();
        target.Dispatcher.InvokeAsync(async () =>
        {
            var rows = new List<Measurement>();
            PickerOverlayWindow? overlay = null;
            MagnifierWindow? lens = null;
            try
            {
                using var samplerLifetime = new SamplerLifetime();
                var sampler = samplerLifetime.Sampler;
                await Task.Delay(500);
                Measure("before", target, sampler, rows);
                overlay = new PickerOverlayWindow();
                overlay.Show();
                await Task.Delay(250);
                Measure("overlay", target, sampler, rows);
                lens = new MagnifierWindow();
                lens.Show();
                // Deliberately overlap the target to detect lens feedback, too.
                var p = target.PointToScreen(new Point(70, 70));
                OverlayPlacement.PlaceAt(new System.Windows.Interop.WindowInteropHelper(lens).Handle,
                    (int)p.X, (int)p.Y);
                await Task.Delay(250);
                Measure("overlay+lens", target, sampler, rows);
                var comparisonPassed = rows.All(row => row.Success) && rows
                    .GroupBy(row => (row.Sample, row.Side))
                    .All(group => group.Select(row => (row.Pixel, row.Selected)).Distinct().Count() == 1);
                var extended = await ExtendedValidation.Run(target, lens, sampler);
                var overlayExclusion = overlay.CaptureExclusionStatus;
                var lensExclusion = lens.CaptureExclusionStatus;
                lens.Close(); lens = null;
                overlay.Close(); overlay = null;
                var lifecycle = await CoordinatorValidation.Run();
                File.WriteAllText(output, JsonSerializer.Serialize(new
                {
                    TimestampUtc = DateTime.UtcNow,
                    Os = Environment.OSVersion.ToString(),
                    Reference = "_about/exemplo.svg: #3A10E0",
                    OverlayExclusion = overlayExclusion,
                    LensExclusion = lensExclusion,
                    ComparisonPassed = comparisonPassed,
                    ExtendedValidation = extended,
                    CoordinatorValidation = lifecycle,
                    Measurements = rows
                }).Replace("{\"Stage\":", "\n{\"Stage\":"));
                Console.WriteLine(Path.GetFullPath(output));
                if (!comparisonPassed) throw new InvalidOperationException("Capture changed after overlay/lens composition.");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                Environment.ExitCode = 1;
            }
            finally
            {
                lens?.Close();
                overlay?.Close();
                target.Close();
                app.Shutdown();
            }
        });
        app.Run();
    }

    private static Window CreateTarget()
    {
        // Unscaled 32-bit raster: one source pixel maps to one physical screen pixel.
        const int width = 320, height = 220;
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var color = y switch
            {
                < 80 => new RgbColor(0x3A, 0x10, 0xE0),
                < 120 => x < 160 ? new RgbColor(0x3A, 0x10, 0xE0) : new RgbColor(255, 255, 255),
                < 160 => x == 160 ? new RgbColor(0x3A, 0x10, 0xE0) : new RgbColor(255, 255, 255),
                _ => new RgbColor((byte)x, (byte)(x / 2), (byte)(255 - x / 2))
            };
            var i = (y * width + x) * 4;
            bytes[i] = color.Blue; bytes[i + 1] = color.Green; bytes[i + 2] = color.Red;
        }
        const double dpi = 96d;
        var bitmap = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgr32, null, bytes, width * 4);
        var image = new Image { Source = bitmap, Stretch = Stretch.None };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var window = new Window
        {
            Title = "TMHue — sampling diagnostics", WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, Width = width, Height = height,
            Left = 100, Top = 100, Topmost = true, Content = image
        };
        window.SourceInitialized += (_, _) =>
        {
            var scale = VisualTreeHelper.GetDpi(window);
            window.Width = width / scale.DpiScaleX;
            window.Height = height / scale.DpiScaleY;
            image.Width = window.Width; image.Height = window.Height;
            image.Stretch = Stretch.Fill;
        };
        return window;
    }

    private static void Measure(string stage, Window target, ScreenColorSampler sampler, List<Measurement> rows)
    {
        var dpi = VisualTreeHelper.GetDpi(target);
        foreach (var (name, x, y) in new[]
        {
            ("solid", 100, 40), ("edge", 159, 100), ("thin-line", 160, 140),
            ("gradient", 100, 180), ("lens-overlap", 100, 70)
        })
        {
            var p = target.PointToScreen(new Point(x / dpi.DpiScaleX, y / dpi.DpiScaleY));
            foreach (var side in new[] { 1, 5, 11 })
            {
                var success = sampler.TryReadRegion((int)p.X, (int)p.Y, side, out var sample);
                var representative = success && sample!.TrySelectColor(side, out var chosen)
                    ? chosen.ToHex() : null;
                var pixel = success ? sample!.Pixels[side / 2, side / 2].ToHex() : null;
                rows.Add(new Measurement(stage, name, side, (int)p.X, (int)p.Y, success, pixel,
                    representative, success ? RgbColor.MedianOf(sample!.Pixels, side).ToHex() : null));
            }
        }
    }

    private sealed record Measurement(string Stage, string Sample, int Side, int X, int Y,
        bool Success, string? Pixel, string? Selected, string? Median);

    private sealed class SamplerLifetime : IDisposable
    {
        public ScreenColorSampler Sampler { get; } = new();
        public void Dispose() { if ((object)Sampler is IDisposable disposable) disposable.Dispose(); }
    }
}

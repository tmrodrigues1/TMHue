using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TMHue.App.ViewModels;

namespace TMHue.App.Views;

public partial class PaletteExtractorWindow : Window
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"];

    private PaletteExtractorViewModel ViewModel => (PaletteExtractorViewModel)DataContext;

    public PaletteExtractorWindow(PaletteExtractorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Privacy guarantee: image pixels and the derived palette live only while this window
        // is open. Nothing is written to disk anywhere in this feature (loads are stream-based,
        // bypassing WPF's process-wide image cache); dropping the references here lets the GC
        // reclaim the in-memory bitmaps as soon as the window closes.
        Closed += (_, _) => viewModel.Clear();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            OnCloseClick(this, new RoutedEventArgs());
        };
    }

    private void OnClearImageClick(object sender, RoutedEventArgs e) => ViewModel.Clear();

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    // Deferred close for the same borderless-over-owner click-fallthrough reason documented in
    // ContrastCheckerWindow.OnCloseClick.
    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = GetDroppedImagePath(e) is not null
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (GetDroppedImagePath(e) is { } path)
            ViewModel.LoadFromFile(path);
    }

    private static string? GetDroppedImagePath(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return null;
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0) return null;

        var path = files[0];
        return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
            ? path
            : null;
    }

    /// <summary>Hides this modal (and its owner) so they don't cover — or end up inside — the
    /// capture, lets the user rubber-band a region, grabs those physical pixels with a single
    /// local BitBlt, and feeds them to the view model. Nothing is written to disk or uploaded.</summary>
    private async void OnCaptureRegionClick(object sender, RoutedEventArgs e)
    {
        var owner = Owner;
        Hide();
        owner?.Hide();

        try
        {
            // Give the compositor a moment to actually remove the hidden windows from screen
            // before the dimmed selection overlay appears over the desktop.
            await System.Threading.Tasks.Task.Delay(150);

            var selector = new RegionCaptureWindow();
            selector.ShowDialog();

            if (selector.Selection is not { } region) return;

            // The selection overlay closes before capture, so the shot contains only the screen.
            await System.Threading.Tasks.Task.Delay(100);

            var capture = CaptureScreenRegion(region.X, region.Y, region.Width, region.Height);
            if (capture is not null)
                ViewModel.LoadFromCapture(capture);
        }
        finally
        {
            owner?.Show();
            Show();
            Activate();
        }
    }

    private static BitmapSource? CaptureScreenRegion(int x, int y, int width, int height)
    {
        try
        {
            using var bitmap = new System.Drawing.Bitmap(width, height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            }

            // PNG round-trip through memory: simplest lossless GDI bitmap -> BitmapSource bridge
            // without an HBITMAP handle to manually release. One-shot, so the copy cost is fine.
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            source.Freeze();
            return source;
        }
        catch
        {
            // Secure/off-screen regions can fail the BitBlt; the modal simply stays as it was.
            return null;
        }
    }
}

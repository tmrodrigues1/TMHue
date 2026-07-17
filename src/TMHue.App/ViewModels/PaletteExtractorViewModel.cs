using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;

namespace TMHue.App.ViewModels;

/// <summary>Backs the palette extraction window: an image (dropped file or screen-region
/// capture) is decoded, downsampled and quantized locally into up to 15 dominant colors.
/// Nothing is ever uploaded — decoding and median-cut both run in-process.</summary>
public sealed class PaletteExtractorViewModel : ViewModelBase
{
    // Rendered as three rows of five in the window.
    private const int PaletteSize = 15;

    // Downsampling cap: 128x128 (≤16k pixels) is plenty for a dominant palette and keeps
    // extraction instant even for wallpapers-sized sources.
    private const int MaxSampleDimension = 128;

    // Decode cap for dropped files: the preview area is small and the palette sampling tops out
    // at 128px anyway, so decoding a wallpaper-sized photo at full resolution only inflates RAM
    // (a 4K JPEG is ~33 MB decoded; capped at 1024 wide it's ~3 MB) and decode CPU. Never
    // upscales — images narrower than this decode at their natural size.
    private const int MaxPreviewDecodeWidth = 1024;

    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;
    private readonly Func<AppSettings> _settingsAccessor;

    public PaletteExtractorViewModel(
        IClipboardService clipboard,
        INotificationService notifications,
        Func<AppSettings> settingsAccessor)
    {
        _clipboard = clipboard;
        _notifications = notifications;
        _settingsAccessor = settingsAccessor;
        CopySwatchCommand = new RelayCommand<HarmonySwatch>(CopySwatch);
    }

    public RelayCommand<HarmonySwatch> CopySwatchCommand { get; }

    public ObservableCollection<HarmonySwatch> Palette { get; } = new();

    private ImageSource? _previewImage;
    public ImageSource? PreviewImage { get => _previewImage; private set => SetField(ref _previewImage, value); }

    public bool HasImage => PreviewImage is not null;

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    /// <summary>Loads an image the user dropped onto the window. Decoding happens locally via
    /// WPF's own decoders; the file is read once and never leaves the machine. Loaded through a
    /// stream (not UriSource) deliberately: WPF caches URI-loaded bitmaps process-wide, and this
    /// feature guarantees no image data lingers anywhere after the window is cleared/closed —
    /// it also means dropping the same file again always re-reads the current file contents.</summary>
    public void LoadFromFile(string path)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(path);

            // Header-only pass to learn the natural width without decoding any pixels, so the
            // decode cap below never upscales small images.
            var naturalWidth = BitmapDecoder.Create(
                stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0].PixelWidth;
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            if (naturalWidth > MaxPreviewDecodeWidth)
                bitmap.DecodePixelWidth = MaxPreviewDecodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();

            SetSource(bitmap);
        }
        catch
        {
            ErrorMessage = Infrastructure.LocalizationService.Get("L.Palette.ReadError");
        }
    }

    /// <summary>Drops the loaded image and its palette so the user can start over — and, for
    /// privacy, so no pixel data outlives the feature's use (also called when the window closes).</summary>
    public void Clear()
    {
        PreviewImage = null;
        Palette.Clear();
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasImage));
    }

    /// <summary>Loads pixels captured from a screen region (already a BitmapSource).</summary>
    public void LoadFromCapture(BitmapSource capture) => SetSource(capture);

    private void SetSource(BitmapSource source)
    {
        ErrorMessage = null;
        PreviewImage = source;
        OnPropertyChanged(nameof(HasImage));

        var pixels = SamplePixels(source);
        var colors = PaletteExtractor.Extract(pixels, PaletteSize);

        var format = _settingsAccessor().CopyFormat;
        Palette.Clear();
        foreach (var color in colors)
            Palette.Add(new HarmonySwatch(color, format));

        if (Palette.Count == 0)
            ErrorMessage = Infrastructure.LocalizationService.Get("L.Palette.NoColors");
    }

    /// <summary>Downscales the source so the longest side is at most
    /// <see cref="MaxSampleDimension"/> px, converts to BGRA and flattens into RGB samples,
    /// skipping mostly-transparent pixels (their RGB is meaningless for dominance).</summary>
    private static IReadOnlyList<RgbColor> SamplePixels(BitmapSource source)
    {
        var scale = Math.Min(1.0, MaxSampleDimension / (double)Math.Max(source.PixelWidth, source.PixelHeight));
        BitmapSource sampled = scale < 1.0
            ? new TransformedBitmap(source, new ScaleTransform(scale, scale))
            : source;

        var converted = new FormatConvertedBitmap(sampled, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var buffer = new byte[stride * height];
        converted.CopyPixels(buffer, stride, 0);

        var pixels = new List<RgbColor>(width * height);
        for (var i = 0; i < buffer.Length; i += 4)
        {
            if (buffer[i + 3] < 128) continue; // mostly transparent
            pixels.Add(new RgbColor(buffer[i + 2], buffer[i + 1], buffer[i]));
        }
        return pixels;
    }

    // Same confirmation surface as a screen capture: the centered Windows toast.
    private void CopySwatch(HarmonySwatch? swatch)
    {
        if (swatch is null) return;
        if (_clipboard.TrySetText(swatch.Value))
            _notifications.ShowCaptureConfirmation(swatch.Color.ToHex(), swatch.Value);
    }
}

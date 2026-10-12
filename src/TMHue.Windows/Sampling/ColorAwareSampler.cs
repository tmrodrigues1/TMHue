using System.IO;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;

namespace TMHue.Windows.Sampling;

/// <summary>Per-session metadata and optional SDR ICC conversion. Rendered output leaves
/// every channel untouched. Advanced-color content is never presented as corrected sRGB.</summary>
public sealed class ColorAwareSampler : IScreenColorSampler, IDisposable
{
    private readonly IScreenColorSampler _source;
    private readonly CaptureColorOutput _output;
    private readonly MonitorLayout _layout;
    // Per monitor, aligned with _layout.Monitors and resolved once: the picker reads up to 30
    // times per second, and neither the metadata nor its description changes within a session.
    private readonly MonitorSource[] _sources;
    private readonly Dictionary<string, IccColorTransform> _transforms = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _frozen;
    private bool _disposed;
    public bool ColorManagementFailed { get; private set; }
    public IReadOnlyList<DisplayColorInfo> Environment { get; }

    public ColorAwareSampler(IScreenColorSampler source, CaptureColorOutput output, MonitorLayout layout,
        IReadOnlyList<DisplayColorInfo> environment, string? srgbProfile)
    {
        _source = source; _output = output; _layout = layout;
        _frozen = source is FrozenScreenSampler;
        Environment = environment;
        var metadata = environment.ToDictionary(m => m.DeviceName, StringComparer.OrdinalIgnoreCase);
        _sources = layout.Monitors.Select(monitor =>
        {
            metadata.TryGetValue(monitor.DeviceName, out var info);
            return new MonitorSource(info, Describe(info));
        }).ToArray();
        if (output != CaptureColorOutput.Srgb || srgbProfile is null) return;
        foreach (var info in environment)
        {
            if (info.AdvancedColorActive is not false || info.ProfilePath is null) continue;
            var transform = IccColorTransform.TryCreate(info.ProfilePath, srgbProfile);
            if (transform is not null) _transforms.Add(info.DeviceName, transform);
        }
    }

    public bool TryReadRegion(int x, int y, int side, out ScreenSample? sample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ColorManagementFailed = false;
        if (!_source.TryReadRegion(x, y, side, out sample) || sample is null) return false;
        var center = IndexOfMonitorAt(x, y);
        if (center < 0) { sample.Clear(); sample = null; return false; }
        var source = _sources[center];
        var left = x - side / 2; var top = y - side / 2;
        if (_output == CaptureColorOutput.Srgb)
        {
            foreach (var monitor in _layout.Monitors)
            {
                var bounds = monitor.Bounds;
                if (left >= bounds.Right || left + side <= bounds.Left || top >= bounds.Bottom || top + side <= bounds.Top) continue;
                if (!_transforms.TryGetValue(monitor.DeviceName, out var transform)
                    || !transform.TryConvert(sample, bounds, left, top))
                {
                    sample.Clear(); sample = null; ColorManagementFailed = true;
                    return false;
                }
            }
        }
        sample.SourceDescription = source.Description;
        sample.ColorOutput = _output; sample.AdvancedColorActive = source.Info?.AdvancedColorActive; sample.IsFrozen = _frozen;
        return true;
    }

    private sealed record MonitorSource(DisplayColorInfo? Info, string Description);

    private string Describe(DisplayColorInfo? info)
    {
        var mode = info?.AdvancedColorActive switch { true => "HDR/WCG · 8-bit limited", false => "SDR", _ => "display mode unknown" };
        var description = _output == CaptureColorOutput.Srgb
            ? $"sRGB · ICC ({Path.GetFileName(info?.ProfilePath)})"
            : $"GDI · RGB 8-bit · {mode}";
        return _frozen ? description + " · frozen" : description;
    }

    private int IndexOfMonitorAt(int x, int y)
    {
        var monitors = _layout.Monitors;
        for (var i = 0; i < monitors.Count; i++)
            if (monitors[i].Bounds.Contains(x, y)) return i;
        return -1;
    }

    public bool TryReadPixel(int x, int y, out RgbColor color)
    {
        color = default;
        return TryReadRegion(x, y, 1, out var sample) && sample!.TrySelectColor(1, out color);
    }

    public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, out (int X, int Y) result) =>
        _source.TryFindNextColorChange(x, y, dx, dy, steps, out result);
    public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, int tolerance, out (int X, int Y) result) =>
        _source.TryFindNextColorChange(x, y, dx, dy, steps, tolerance, out result);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var transform in _transforms.Values) transform.Dispose();
        _transforms.Clear();
        // The live sampler is owned by DI; snapshots are owned by this session.
        if (_source is FrozenScreenSampler snapshot) snapshot.Dispose();
    }
}

using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;

namespace TMHue.Core.Services;

/// <summary>One selection, synchronously used on the UI thread. Confirmation always reads
/// again and seals the session before callers touch clipboard/history or pump messages.</summary>
public sealed class ColorSelectionSession(IScreenColorSampler sampler, int sampleSide)
{
    private bool _sealed;
    public int SampleSide { get; } = sampleSide is 1 or 5 or 11 ? sampleSide : 1;
    public RgbColor? Latest { get; private set; }
    public string? SourceDescription { get; private set; }

    public bool TryPreview(int x, int y, int displaySide, out ScreenSample? sample)
    {
        sample = null;
        if (_sealed) return false;
        return TryRead(x, y, displaySide, out sample);
    }

    public bool TryConfirm(int x, int y, out RgbColor color)
    {
        color = default;
        if (_sealed) return false;
        _sealed = true;
        if (!TryRead(x, y, SampleSide, out _) || Latest is not { } selected) return false;
        color = selected;
        return true;
    }

    private bool TryRead(int x, int y, int displaySide, out ScreenSample? sample)
    {
        Latest = null;
        SourceDescription = null;
        if (!sampler.TryReadRegion(x, y, Math.Max(SampleSide, displaySide), out sample)
            || sample is null || !sample.TrySelectColor(SampleSide, out var color)) return false;
        Latest = color;
        SourceDescription = sample.SourceDescription;
        return true;
    }
}

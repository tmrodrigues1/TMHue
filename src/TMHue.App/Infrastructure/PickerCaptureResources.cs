using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Windows.Display;
using TMHue.Windows.Sampling;

namespace TMHue.App.Infrastructure;

internal sealed class PickerCaptureResources : IDisposable
{
    public ColorAwareSampler? Sampler { get; private set; }
    public FrozenScreenSampler? Snapshot { get; private set; }
    public string? ErrorResource { get; private set; }
    private ScreenColorSampler? _liveSession;
    private MonitorLayout? _layout;

    public static PickerCaptureResources Prepare(IScreenColorSampler live, MonitorLayout layout,
        CaptureColorOutput output, bool freeze, CancellationToken cancellation = default)
    {
        var result = new PickerCaptureResources();
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var environment = DisplayColorEnvironment.Read(layout);
            cancellation.ThrowIfCancellationRequested();
            if (freeze)
            {
                if (!FrozenScreenSampler.TryCreate(layout, out var snapshot, cancellation))
                { result.ErrorResource = "L.Capture.FreezeFailed"; return result; }
                result.Snapshot = snapshot;
            }
            if (result.Snapshot is null && live is ScreenColorSampler liveSampler)
            {
                // The layout is fixed for the whole session (a topology change ends the capture).
                liveSampler.BeginSession(layout);
                result._liveSession = liveSampler; result._layout = layout;
            }
            result.Sampler = new(result.Snapshot is { } frozen ? frozen : live, output, layout,
                environment, output == CaptureColorOutput.Srgb ? DisplayColorEnvironment.GetSrgbProfilePath() : null);
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_liveSession is not null && _layout is not null) _liveSession.EndSession(_layout);
        _liveSession = null; _layout = null;
        Sampler?.Dispose(); Sampler = null;
        Snapshot?.Dispose(); Snapshot = null;
    }
}

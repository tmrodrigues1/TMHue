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
        Sampler?.Dispose(); Sampler = null;
        Snapshot?.Dispose(); Snapshot = null;
    }
}

using System.Runtime;
using TMHue.Windows.Native;

namespace TMHue.Windows.Memory;

/// <summary>Hands memory back to the system once the app goes quiet (window sent to the tray,
/// a tool window closed, a capture finished). A tray-resident process otherwise keeps its peak
/// footprint indefinitely, because nothing allocates enough afterwards to trigger a collection.</summary>
public sealed class MemoryTrimmer : IDisposable
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    public MemoryTrimmer()
    {
        _timer = new System.Threading.Timer(_ => Trim(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Restarts the idle countdown; the trim runs once, after the last call has been
    /// quiet for a few seconds. Call from the UI thread.</summary>
    public void ScheduleTrim()
    {
        if (_disposed) return;
        _timer.Change(IdleDelay, Timeout.InfiniteTimeSpan);
    }

    internal static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        // Native buffers (frozen-screen sections, decoded bitmaps) are only released by
        // finalizers; the second pass reclaims what they were holding.
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        // (-1, -1) asks Windows to page out everything the process is not actively using.
        NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), -1, -1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}

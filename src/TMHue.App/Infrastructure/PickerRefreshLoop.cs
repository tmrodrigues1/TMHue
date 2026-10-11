using System.Windows.Threading;
using System.Diagnostics;

namespace TMHue.App.Infrastructure;

/// <summary>Up to 30 live reads/second while moving, 10 while stationary. Frozen
/// input is coalesced at render priority without polling. All work belongs to one dispatcher.</summary>
internal sealed class PickerRefreshLoop : IDisposable
{
    private static readonly TimeSpan MovingInterval = TimeSpan.FromMilliseconds(1000d / 30);
    private static readonly TimeSpan StationaryInterval = TimeSpan.FromMilliseconds(100);
    private readonly Action<int, int> _read;
    private readonly DispatcherTimer? _timer;
    private DispatcherOperation? _pending;
    private (int X, int Y) _position;
    private bool _disposed;
    private long _lastMove = Stopwatch.GetTimestamp();

    public PickerRefreshLoop(bool frozen, int x, int y, Action<int, int> read)
    {
        _read = read; _position = (x, y);
        if (frozen) return;
        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = MovingInterval
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Move(int x, int y)
    {
        if (_disposed || _position == (x, y)) return;
        _position = (x, y);
        _lastMove = Stopwatch.GetTimestamp();
        // Changing Interval reschedules the dispatcher timer. Reassigning it on every
        // mouse event postpones reads indefinitely during continuous movement.
        if (_timer is not null && _timer.Interval != MovingInterval) _timer.Interval = MovingInterval;
        if (_timer is not null || _pending is not null) return;
        _pending = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _pending = null;
            if (!_disposed) _read(_position.X, _position.Y);
        });
    }

    public void ReadNow(int x, int y)
    {
        if (_disposed) return;
        _pending?.Abort(); _pending = null; _position = (x, y);
        _timer?.Stop();
        _read(x, y);
        if (!_disposed) _timer?.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (Stopwatch.GetElapsedTime(_lastMove).TotalMilliseconds >= 200
            && _timer is not null && _timer.Interval != StationaryInterval)
            _timer.Interval = StationaryInterval;
        _read(_position.X, _position.Y);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pending?.Abort(); _pending = null;
        if (_timer is null) return;
        _timer.Stop(); _timer.Tick -= OnTick;
    }
}

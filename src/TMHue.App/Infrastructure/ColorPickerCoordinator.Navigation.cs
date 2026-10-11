using TMHue.Core.Models;
using TMHue.Windows.Display;

namespace TMHue.App.Infrastructure;

public sealed partial class ColorPickerCoordinator
{
    private void OnArrowKeyPressed(object? sender, (int Dx, int Dy, bool JumpToColorChange) e)
    {
        if (State != PickerState.Picking || _layout is null || _resources?.Sampler is not { } sampler) return;
        try
        {
            var (x, y) = OverlayPlacement.GetPhysicalCursorPosition();
            var target = _layout.LimitMovement(x, y, e.Dx, e.Dy, e.JumpToColorChange ? 80 : 1);
            if (e.JumpToColorChange)
            {
                var steps = Math.Max(Math.Abs(target.X - x), Math.Abs(target.Y - y));
                if (steps <= 0) return;
                if (sampler.TryFindNextColorChange(x, y, e.Dx, e.Dy, steps, _colorTolerance, out var found)) target = found;
            }
            if (target == (x, y)) return;
            OverlayPlacement.SetCursorPosition(target.X, target.Y);
            var actual = OverlayPlacement.GetPhysicalCursorPosition();
            _refresh?.ReadNow(actual.X, actual.Y);
        }
        catch { FailCapture("L.Capture.ReadFailed"); }
    }
}

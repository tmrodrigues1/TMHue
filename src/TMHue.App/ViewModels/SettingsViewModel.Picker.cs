namespace TMHue.App.ViewModels;

public sealed partial class SettingsViewModel
{
    public bool UsePrecisionCursor
    {
        get => _settings.UsePrecisionCursor;
        set { _settings.UsePrecisionCursor = value; OnPropertyChanged(); Persist(); }
    }
    public bool FreezeScreenWhilePicking
    {
        get => _settings.FreezeScreenWhilePicking;
        set { _settings.FreezeScreenWhilePicking = value; OnPropertyChanged(); Persist(); }
    }
    public int ColorChangeTolerance
    {
        get => _settings.ColorChangeTolerance;
        set { _settings.ColorChangeTolerance = Math.Clamp(value, 0, 32); OnPropertyChanged(); Persist(); }
    }
    public int SelectedColorOutputIndex
    {
        get => (int)_settings.ColorOutput;
        set
        {
            if (value is < 0 or > 1) return;
            _settings.ColorOutput = (TMHue.Core.Models.CaptureColorOutput)value;
            OnPropertyChanged(); Persist();
        }
    }
}

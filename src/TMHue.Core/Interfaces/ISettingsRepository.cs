using TMHue.Core.Models;

namespace TMHue.Core.Interfaces;

public interface ISettingsRepository
{
    AppSettings Load();

    void Save(AppSettings settings);
}

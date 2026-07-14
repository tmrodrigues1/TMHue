using System.IO;
using TMHue.Windows.Persistence;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"tmhue-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_OversizedFile_DiscardsItAndReturnsDefaults()
    {
        File.WriteAllText(_tempFile, new string('x', 1024 * 1024 + 1));
        var repository = new SettingsRepository(_tempFile);

        var settings = repository.Load();

        Assert.NotNull(settings);
        Assert.False(File.Exists(_tempFile));
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}

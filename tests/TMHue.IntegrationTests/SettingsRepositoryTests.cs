using System.IO;
using TMHue.Windows.Persistence;
using TMHue.Core.Models;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"tmhue-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void NewSettings_DefaultToExactPixel()
    {
        Assert.Equal(1, new SettingsRepository(_tempFile).Load().SampleAreaSize);
        Assert.Equal(1, AppSettings.CreateDefault().SampleAreaSize);
        var settings = new SettingsRepository(_tempFile).Load();
        Assert.False(settings.FreezeScreenWhilePicking);
        Assert.False(settings.UsePrecisionCursor);
        Assert.Equal(CaptureColorOutput.Rendered, settings.ColorOutput);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void ExistingSettings_PreserveSamplingPreference(int side)
    {
        var repository = new SettingsRepository(_tempFile);
        repository.Save(new AppSettings { SampleAreaSize = side });
        Assert.Equal(side, repository.Load().SampleAreaSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(999999)]
    public void InvalidSampleSize_NormalizesToExactPixel(int side)
    {
        File.WriteAllText(_tempFile, $"{{\"SampleAreaSize\":{side}}}");
        Assert.Equal(1, new SettingsRepository(_tempFile).Load().SampleAreaSize);
    }

    [Fact]
    public void PickerOptions_RoundTripWithoutChangingSamplingPreference()
    {
        var repository = new SettingsRepository(_tempFile);
        repository.Save(new AppSettings { SampleAreaSize = 11, FreezeScreenWhilePicking = true,
            UsePrecisionCursor = true, ColorChangeTolerance = 8, ColorOutput = CaptureColorOutput.Srgb });
        var settings = repository.Load();
        Assert.Equal(11, settings.SampleAreaSize); Assert.True(settings.FreezeScreenWhilePicking);
        Assert.True(settings.UsePrecisionCursor); Assert.Equal(8, settings.ColorChangeTolerance);
        Assert.Equal(CaptureColorOutput.Srgb, settings.ColorOutput);
    }

    [Fact]
    public void InvalidToleranceAndOutput_AreNormalized()
    {
        File.WriteAllText(_tempFile, "{\"ColorChangeTolerance\":999,\"ColorOutput\":999}");
        var settings = new SettingsRepository(_tempFile).Load();
        Assert.Equal(32, settings.ColorChangeTolerance);
        Assert.Equal(CaptureColorOutput.Rendered, settings.ColorOutput);
    }

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

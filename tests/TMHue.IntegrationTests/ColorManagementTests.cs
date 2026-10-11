using System.IO;
using System.Runtime.InteropServices;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;
using TMHue.Windows.Native;
using TMHue.Windows.Sampling;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class ColorManagementTests
{
    private static readonly RgbColor Blue = new(58, 16, 224);
    private static MonitorLayout Layout => new(new[] { new MonitorArea("test", new(0, 0, 100, 100)) });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void RenderedOutput_DoesNotChangeChannelsRegardlessOfDisplayMode(bool? advanced)
    {
        using var sampler = new ColorAwareSampler(new FakeSampler(), CaptureColorOutput.Rendered, Layout,
            new[] { new DisplayColorInfo("test", advanced, 10, null) }, null);
        Assert.True(sampler.TryReadRegion(50, 50, 11, out var sample));
        Assert.True(sample!.TrySelectColor(1, out var color));
        Assert.Equal(Blue, color);
        Assert.Equal(advanced, sample.AdvancedColorActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void SrgbOutput_RejectsHdrAndUnknownMetadata(bool? advanced)
    {
        var source = new FakeSampler();
        using var sampler = new ColorAwareSampler(source, CaptureColorOutput.Srgb, Layout,
            new[] { new DisplayColorInfo("test", advanced, 10, DisplayColorEnvironment.GetSrgbProfilePath()) },
            DisplayColorEnvironment.GetSrgbProfilePath());
        Assert.False(sampler.TryReadRegion(50, 50, 11, out var failed));
        Assert.Null(failed); Assert.True(sampler.ColorManagementFailed);
        Assert.All(source.Sample.Valid.Cast<bool>(), valid => Assert.False(valid));
    }

    [Fact]
    public void SrgbOutput_RejectsMissingProfileInsteadOfRelabelingRawPixels()
    {
        var source = new FakeSampler();
        using var sampler = new ColorAwareSampler(source, CaptureColorOutput.Srgb, Layout,
            new[] { new DisplayColorInfo("test", false, 8, null) }, DisplayColorEnvironment.GetSrgbProfilePath());
        Assert.False(sampler.TryReadRegion(50, 50, 11, out var sample));
        Assert.Null(sample);
        Assert.True(sampler.ColorManagementFailed);
        Assert.All(source.Sample.Valid.Cast<bool>(), valid => Assert.False(valid));
    }

    [Fact]
    public void IccSrgbIdentity_PreservesReferenceAndMatchesNativeTransform()
    {
        var profile = DisplayColorEnvironment.GetSrgbProfilePath();
        Assert.NotNull(profile);
        using var identity = IccColorTransform.TryCreate(profile, profile);
        Assert.NotNull(identity);
        var sample = new FakeSampler().Sample;
        Assert.True(identity.TryConvert(sample, new(0, 0, 100, 100), 45, 45));
        Assert.Equal(Blue, sample.Pixels[5, 5]);
        // A copied ICC profile forces native transform creation rather than the identity shortcut.
        var copy = Path.Combine(Path.GetTempPath(), $"tmhue-profile-{Guid.NewGuid():N}.icm");
        File.Copy(profile, copy);
        try
        {
            using var native = IccColorTransform.TryCreate(copy, profile);
            Assert.NotNull(native);
            Assert.True(native.TryConvert(sample, new(0, 0, 100, 100), 45, 45));
            var result = sample.Pixels[5, 5];
            Assert.InRange(Math.Abs(result.Red - Blue.Red), 0, 1);
            Assert.InRange(Math.Abs(result.Green - Blue.Green), 0, 1);
            Assert.InRange(Math.Abs(result.Blue - Blue.Blue), 0, 1);
        }
        finally { File.Delete(copy); }
    }

    [Fact]
    public void NativeStructures_MatchWindowsSdkX64Layouts()
    {
        Assert.Equal(20, Marshal.SizeOf<DisplayColorNative.Header>());
        Assert.Equal(20, Marshal.SizeOf<DisplayColorNative.Source>());
        Assert.Equal(48, Marshal.SizeOf<DisplayColorNative.Target>());
        Assert.Equal(72, Marshal.SizeOf<DisplayColorNative.Path>());
        Assert.Equal(32, Marshal.SizeOf<DisplayColorNative.AdvancedColor>());
        Assert.Equal(36, Marshal.SizeOf<DisplayColorNative.AdvancedColor2>());
        Assert.Equal(16, Marshal.SizeOf<IccNative.Color>());
        Assert.Equal(24, Marshal.SizeOf<IccNative.Profile>());
    }

    private sealed class FakeSampler : IScreenColorSampler
    {
        public ScreenSample Sample { get; } = new(11);
        public FakeSampler()
        {
            for (var row = 0; row < 11; row++)
            for (var col = 0; col < 11; col++) { Sample.Pixels[row, col] = Blue; Sample.Valid[row, col] = true; }
        }
        public bool TryReadRegion(int x, int y, int side, out ScreenSample? sample) { sample = Sample; return true; }
        public bool TryReadPixel(int x, int y, out RgbColor color) { color = Blue; return true; }
        public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, out (int X, int Y) result)
        { result = default; return false; }
    }
}

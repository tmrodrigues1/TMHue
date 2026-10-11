using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public sealed class ColorSelectionSessionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void Confirm_RereadsClickInsteadOfUsingPreview(int side)
    {
        var sampler = new FakeSampler();
        var session = new ColorSelectionSession(sampler, side);
        Assert.True(session.TryPreview(10, 20, 11, out _));
        Assert.True(session.TryConfirm(100, 200, out var color));
        Assert.Equal(new RgbColor(100, 200, 0), color);
        Assert.Equal((100, 200, side), sampler.LastRead);
        Assert.False(session.TryPreview(30, 40, 11, out _));
        Assert.Equal(color, session.Latest); // queued preview cannot replace confirmation
        Assert.False(session.TryConfirm(30, 40, out _));
    }

    [Fact]
    public void ImmediateClick_DoesNotRequirePreviousMovement()
    {
        var session = new ColorSelectionSession(new FakeSampler(), 1);
        Assert.True(session.TryConfirm(58, 16, out var color));
        Assert.Equal(new RgbColor(58, 16, 0), color);
    }

    [Fact]
    public void FailedConfirm_DiscardsPreviewAndSealsSession()
    {
        var sampler = new FakeSampler();
        var session = new ColorSelectionSession(sampler, 5);
        session.TryPreview(10, 20, 11, out _);
        sampler.Fail = true;
        Assert.False(session.TryConfirm(100, 200, out var color));
        Assert.Equal(default, color);
        Assert.Null(session.Latest);
        sampler.Fail = false;
        Assert.False(session.TryPreview(10, 20, 11, out _));
    }

    [Fact]
    public void FailedPreview_DiscardsPreviousColor()
    {
        var sampler = new FakeSampler();
        var session = new ColorSelectionSession(sampler, 1);
        session.TryPreview(10, 20, 1, out _);
        sampler.Fail = true;
        Assert.False(session.TryPreview(30, 40, 1, out _));
        Assert.Null(session.Latest);
    }

    [Fact]
    public void Confirm_RereadsChangedContentAtSamePosition()
    {
        var sampler = new FakeSampler();
        var session = new ColorSelectionSession(sampler, 1);
        session.TryPreview(10, 20, 1, out _);
        sampler.Blue = 224;
        Assert.True(session.TryConfirm(10, 20, out var color));
        Assert.Equal((byte)224, color.Blue);
    }

    [Fact]
    public void RegionalPreviewAndConfirm_UseSameMode()
    {
        var sampler = new FakeSampler { CenterOnly = true };
        var session = new ColorSelectionSession(sampler, 5);
        Assert.True(session.TryPreview(10, 20, 11, out _));
        var preview = session.Latest;
        Assert.True(session.TryConfirm(10, 20, out var selected));
        Assert.Equal(preview, selected);
        Assert.Equal(new RgbColor(255, 255, 255), selected);
    }

    private sealed class FakeSampler : IScreenColorSampler
    {
        public bool Fail { get; set; }
        public bool CenterOnly { get; set; }
        public byte Blue { get; set; }
        public (int X, int Y, int Side) LastRead { get; private set; }
        public bool TryReadRegion(int x, int y, int side, out ScreenSample? sample)
        {
            LastRead = (x, y, side);
            sample = null;
            if (Fail) return false;
            sample = new ScreenSample(side);
            var color = new RgbColor((byte)x, (byte)y, Blue);
            for (var row = 0; row < side; row++)
            for (var col = 0; col < side; col++)
            {
                sample.Valid[row, col] = true;
                sample.Pixels[row, col] = CenterOnly ? new(255, 255, 255) : color;
            }
            sample.Pixels[side / 2, side / 2] = color;
            return true;
        }
        public bool TryReadPixel(int x, int y, out RgbColor color) => throw new NotSupportedException();
        public bool TryFindNextColorChange(int x, int y, int dx, int dy, int maxSteps,
            out (int X, int Y) result) => throw new NotSupportedException();
    }
}

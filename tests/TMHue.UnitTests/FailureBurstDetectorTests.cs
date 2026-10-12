using TMHue.Core.Services;
using Xunit;

namespace TMHue.UnitTests;

public class FailureBurstDetectorTests
{
    [Fact]
    public void RegisterFailure_TripsWhenThresholdFallsInsideTheWindow()
    {
        var detector = new FailureBurstDetector(threshold: 3, TimeSpan.FromSeconds(10));

        Assert.False(detector.RegisterFailure(0));
        Assert.False(detector.RegisterFailure(4_000));
        Assert.True(detector.RegisterFailure(10_000));
    }

    [Fact]
    public void RegisterFailure_ToleratesFailuresSpreadBeyondTheWindow()
    {
        var detector = new FailureBurstDetector(threshold: 3, TimeSpan.FromSeconds(10));

        for (var second = 0; second < 600; second += 6)
            Assert.False(detector.RegisterFailure(second * 1000L));
    }

    [Fact]
    public void RegisterFailure_OnlyCountsTheMostRecentFailures()
    {
        var detector = new FailureBurstDetector(threshold: 3, TimeSpan.FromSeconds(10));
        detector.RegisterFailure(0);
        detector.RegisterFailure(100_000);

        Assert.False(detector.RegisterFailure(100_500));
        Assert.True(detector.RegisterFailure(101_000));
    }
}

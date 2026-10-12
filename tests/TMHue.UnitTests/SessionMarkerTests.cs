using TMHue.Core.Services;
using Xunit;

namespace TMHue.UnitTests;

public class SessionMarkerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"tmhue-session-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _firstStart = new(2026, 10, 11, 9, 30, 0, TimeSpan.FromHours(-3));

    private string MarkerPath => Path.Combine(_folder, "Logs", "session.marker");

    [Fact]
    public void Begin_OnFirstRun_ReportsNothingAndCreatesTheFolder()
    {
        Assert.Null(new SessionMarker(MarkerPath).Begin("1.3.0", _firstStart));
        Assert.True(File.Exists(MarkerPath));
    }

    [Fact]
    public void Begin_AfterACleanExit_ReportsNothing()
    {
        var marker = new SessionMarker(MarkerPath);
        marker.Begin("1.3.0", _firstStart);
        marker.End();

        Assert.Null(new SessionMarker(MarkerPath).Begin("1.3.0", _firstStart.AddHours(1)));
    }

    [Fact]
    public void Begin_AfterASessionThatNeverEnded_ReportsItsVersionAndStartTime()
    {
        new SessionMarker(MarkerPath).Begin("1.2.2", _firstStart);

        var previous = new SessionMarker(MarkerPath).Begin("1.3.0", _firstStart.AddHours(1));

        Assert.Equal(new UncleanSession("1.2.2", _firstStart), previous);
    }

    [Fact]
    public void Begin_ReportsEachUncleanSessionOnlyOnce()
    {
        new SessionMarker(MarkerPath).Begin("1.2.2", _firstStart);
        var second = new SessionMarker(MarkerPath);
        second.Begin("1.3.0", _firstStart.AddHours(1));
        second.End();

        Assert.Null(new SessionMarker(MarkerPath).Begin("1.3.0", _firstStart.AddHours(2)));
    }

    [Fact]
    public void Begin_WithAnUnreadableMarker_StillReportsAnInterruptedSession()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath) ?? _folder);
        File.WriteAllText(MarkerPath, "not a marker");

        var previous = new SessionMarker(MarkerPath).Begin("1.3.0", _firstStart);

        Assert.NotNull(previous);
        Assert.Equal("?", previous.Version);
    }

    [Fact]
    public void End_WithoutAMarker_DoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath) ?? _folder);

        new SessionMarker(MarkerPath).End();
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}

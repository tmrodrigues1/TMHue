using TMHue.Windows.Reliability;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class WindowsCrashReportsTests
{
    [Fact]
    public void ReadSince_AFutureInstant_FindsNothing()
    {
        Assert.Empty(WindowsCrashReports.ReadSince(DateTimeOffset.Now.AddDays(1), "TMHue.exe"));
    }

    [Fact]
    public void ReadSince_AnExecutableThatNeverRan_FindsNothing()
    {
        var reports = WindowsCrashReports.ReadSince(DateTimeOffset.Now.AddDays(-30), $"{Guid.NewGuid():N}.exe");

        Assert.Empty(reports);
    }

    [Fact]
    public void ReadSince_RespectsTheEntryLimit()
    {
        // ".exe" matches every crash report on the machine; only the cap is under test here.
        var reports = WindowsCrashReports.ReadSince(DateTimeOffset.Now.AddDays(-365), ".exe", maxEntries: 2);

        Assert.InRange(reports.Count, 0, 2);
    }
}

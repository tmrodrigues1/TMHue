using TMHue.Core.Services;
using Xunit;

namespace TMHue.UnitTests;

public class ErrorLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"tmhue-log-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);

    private string LogPath => Path.Combine(_folder, "errors.log");
    private string ArchivePath => Path.Combine(_folder, "errors.1.log");

    [Fact]
    public void Append_CreatesTheFolderAndWritesATimestampedEntry()
    {
        new ErrorLog(LogPath).Append("boom", _now);

        Assert.Equal($"[2026-10-11 09:30:00] boom{Environment.NewLine}", File.ReadAllText(LogPath));
    }

    [Fact]
    public void Append_CollapsesIdenticalConsecutiveEntriesIntoACount()
    {
        var log = new ErrorLog(LogPath);

        for (var i = 0; i < 500; i++) log.Append("same failure", _now);
        log.Append("different failure", _now);

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith("same failure", lines[0]);
        Assert.EndsWith("Entrada anterior repetida 499 vez(es).", lines[1]);
        Assert.EndsWith("different failure", lines[2]);
    }

    [Fact]
    public void Flush_WritesThePendingRepeatCountOnce()
    {
        var log = new ErrorLog(LogPath);
        log.Append("same failure", _now);
        log.Append("same failure", _now);

        log.Flush();
        log.Flush();

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("Entrada anterior repetida 1 vez(es).", lines[1]);
    }

    [Fact]
    public void Append_RotatesToASingleArchiveInsteadOfDeletingHistory()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(ArchivePath, "stale archive");
        File.WriteAllText(LogPath, new string('x', (int)ErrorLog.MaxFileBytes));

        new ErrorLog(LogPath).Append("after rotation", _now);

        Assert.Equal(ErrorLog.MaxFileBytes, new FileInfo(ArchivePath).Length);
        Assert.Equal($"[2026-10-11 09:30:00] after rotation{Environment.NewLine}", File.ReadAllText(LogPath));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }

    [Fact]
    public void Append_TruncatesOversizedEntries()
    {
        new ErrorLog(LogPath).Append(new string('e', 100_000), _now);

        Assert.InRange(new FileInfo(LogPath).Length, 16 * 1024, 17 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}

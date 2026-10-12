using TMHue.Core.Models;
using TMHue.Core.Services;
using Xunit;

namespace TMHue.UnitTests;

public class ColorHistoryServiceTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"tmhue-history-{Guid.NewGuid():N}.json");

    [Fact]
    public void Add_KeepsAtMostMaxItems()
    {
        var service = new ColorHistoryService(_tempFile);

        for (var i = 0; i <= ColorHistoryService.MaxItems; i++)
            service.Add(new CapturedColor($"#{i:D6}", 0, 0, (byte)i, DateTimeOffset.Now));

        Assert.Equal(ColorHistoryService.MaxItems, service.Items.Count);
        Assert.DoesNotContain(service.Items, color => color.Hex == "#000000");
    }

    [Fact]
    public void Add_SameHexAsMostRecent_UpdatesInPlaceInsteadOfDuplicating()
    {
        var service = new ColorHistoryService(_tempFile);
        var first = new CapturedColor("#2F80ED", 47, 128, 237, DateTimeOffset.Now);
        var refreshed = new CapturedColor("#2F80ED", 47, 128, 237, DateTimeOffset.Now.AddSeconds(5));

        service.Add(first);
        service.Add(refreshed);

        Assert.Single(service.Items);
        Assert.Equal(refreshed.CapturedAt, service.Items[0].CapturedAt);
    }

    [Fact]
    public void Add_NewestGoesFirst()
    {
        var service = new ColorHistoryService(_tempFile);
        service.Add(new CapturedColor("#111111", 1, 1, 1, DateTimeOffset.Now));
        service.Add(new CapturedColor("#222222", 2, 2, 2, DateTimeOffset.Now));

        Assert.Equal("#222222", service.Items[0].Hex);
    }

    [Fact]
    public void Load_OversizedFile_DiscardsItAndResetsHistory()
    {
        File.WriteAllText(_tempFile, new string('x', 65 * 1024));
        var service = new ColorHistoryService(_tempFile);

        service.Load();

        Assert.Empty(service.Items);
        Assert.False(File.Exists(_tempFile));
    }

    [Fact]
    public void Load_DropsEntriesWithoutAValidHex_AndStillAcceptsNewCaptures()
    {
        File.WriteAllText(_tempFile, """
            [
              { "Hex": "#2F80ED", "Red": 47, "Green": 128, "Blue": 237, "CapturedAt": "2026-10-11T09:00:00+00:00" },
              { "Hex": null, "Red": 1, "Green": 1, "Blue": 1, "CapturedAt": "2026-10-11T09:00:00+00:00" },
              { "Hex": "red", "Red": 255, "Green": 0, "Blue": 0, "CapturedAt": "2026-10-11T09:00:00+00:00" },
              { "Hex": "#12345G", "Red": 0, "Green": 0, "Blue": 0, "CapturedAt": "2026-10-11T09:00:00+00:00" },
              null
            ]
            """);
        var service = new ColorHistoryService(_tempFile);

        service.Load();
        service.Add(new CapturedColor("#111111", 17, 17, 17, DateTimeOffset.Now));

        Assert.Equal(new[] { "#111111", "#2F80ED" }, service.Items.Select(color => color.Hex));
    }

    [Fact]
    public void Load_TruncatesOversizedCaptureSource()
    {
        var source = new string('s', 5000);
        File.WriteAllText(_tempFile,
            $$"""[{ "Hex": "#2F80ED", "Red": 47, "Green": 128, "Blue": 237, "CapturedAt": "2026-10-11T09:00:00+00:00", "CaptureSource": "{{source}}" }]""");
        var service = new ColorHistoryService(_tempFile);

        service.Load();

        Assert.Equal(128, Assert.Single(service.Items).CaptureSource?.Length);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}

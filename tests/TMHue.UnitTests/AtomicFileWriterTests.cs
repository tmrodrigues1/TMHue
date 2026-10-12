using TMHue.Core.Services;
using Xunit;

namespace TMHue.UnitTests;

public class AtomicFileWriterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"tmhue-atomic-{Guid.NewGuid():N}");
    private string Target => Path.Combine(_folder, "settings.json");

    public AtomicFileWriterTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Write_CreatesAndThenReplacesTheTarget_LeavingNoTemporaryFile()
    {
        AtomicFileWriter.Write(Target, "first");
        AtomicFileWriter.Write(Target, "second");

        Assert.Equal("second", File.ReadAllText(Target));
        Assert.Equal(new[] { Target }, Directory.GetFiles(_folder));
    }

    [Fact]
    public void Write_DoesNotDependOnASharedTemporaryName()
    {
        // Another writer mid-flight used to own "<target>.tmp"; a second write must not need it.
        using var otherWriter = new FileStream(Target + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None);

        AtomicFileWriter.Write(Target, "value");

        Assert.Equal("value", File.ReadAllText(Target));
    }

    [Fact]
    public void Write_WhenPromotionFails_RemovesItsTemporaryFileAndKeepsThePreviousContents()
    {
        AtomicFileWriter.Write(Target, "previous");

        using (new FileStream(Target, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => AtomicFileWriter.Write(Target, "next"));

        Assert.Equal("previous", File.ReadAllText(Target));
        Assert.Equal(new[] { Target }, Directory.GetFiles(_folder));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}

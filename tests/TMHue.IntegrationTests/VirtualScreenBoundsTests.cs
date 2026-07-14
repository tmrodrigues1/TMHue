using TMHue.Windows.Display;
using Xunit;

namespace TMHue.IntegrationTests;

public class VirtualScreenBoundsTests
{
    [Fact]
    public void GetCurrent_ReturnsNonEmptyBounds()
    {
        var bounds = VirtualScreenBounds.GetCurrent();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }
}

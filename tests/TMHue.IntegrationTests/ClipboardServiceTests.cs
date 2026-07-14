using System.Runtime.ExceptionServices;
using TMHue.Windows.Clipboard;
using Xunit;

namespace TMHue.IntegrationTests;

/// <summary>
/// Requires an STA thread and a real Windows session (clipboard access), so these are
/// integration, not unit, tests.
/// </summary>
public class ClipboardServiceTests
{
    [Fact]
    public void TrySetText_WritesHexToClipboard()
    {
        var result = RunInSta(() =>
        {
            var service = new ClipboardService();
            var success = service.TrySetText("#2F80ED");
            return (success, text: System.Windows.Clipboard.GetText());
        });

        Assert.True(result.success);
        Assert.Equal("#2F80ED", result.text);
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();

        return result!;
    }
}

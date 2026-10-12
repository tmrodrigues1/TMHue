using System.IO;
using System.IO.Pipes;
using TMHue.Windows.Instancing;
using Xunit;

namespace TMHue.IntegrationTests;

// Synchronous on purpose: the Mutex must be released by the thread that acquired it.
public sealed class SingleInstanceServiceTests
{
    private readonly string _suffix = $".test-{Guid.NewGuid():N}";

    [Fact]
    public void SecondInstance_DeliversItsActionToTheFirst()
    {
        using var first = new SingleInstanceService(_suffix);
        using var received = new ManualResetEventSlim();
        SecondInstanceAction? action = null;
        first.SecondInstanceRequested += (_, requested) => { action = requested; received.Set(); };
        Assert.True(first.TryAcquire());

        using var second = new SingleInstanceService(_suffix);
        Assert.False(second.TryAcquire());
        NotifyUntil(received, SecondInstanceAction.StartCapture);

        Assert.Equal(SecondInstanceAction.StartCapture, action);
    }

    [Fact]
    public void PipeNameHeldByAnotherProcess_BacksOffInsteadOfSpinning()
    {
        using var service = new SingleInstanceService(_suffix);
        using var received = new ManualResetEventSlim();
        service.SecondInstanceRequested += (_, _) => received.Set();

        using (CreateServer(service.PipeName))
        {
            Assert.True(service.TryAcquire());
            Thread.Sleep(TimeSpan.FromSeconds(2));

            // 250 ms, 500 ms, 1 s… the unthrottled loop made ~50,000 attempts per second here.
            Assert.InRange(service.ListenerAttempts, 1, 6);
        }

        // Once the name is free again the listener recovers on its next retry.
        NotifyUntil(received, SecondInstanceAction.OpenWindow);
    }

    [Fact]
    public void SilentClient_IsDroppedAndTheListenerKeepsServing()
    {
        using var service = new SingleInstanceService(_suffix);
        using var received = new ManualResetEventSlim();
        service.SecondInstanceRequested += (_, _) => received.Set();
        Assert.True(service.TryAcquire());

        using var silent = new NamedPipeClientStream(".", service.PipeName, PipeDirection.Out);
        silent.Connect(5000);

        // The stalled connection is still open; only the read timeout can free the listener.
        NotifyUntil(received, SecondInstanceAction.OpenWindow);
    }

    [Theory]
    [InlineData("NotAnAction\n")]
    [InlineData("7\n")]
    [InlineData("")]
    public void InvalidMessage_IsIgnored(string message)
    {
        using var service = new SingleInstanceService(_suffix);
        var requests = 0;
        using var received = new ManualResetEventSlim();
        service.SecondInstanceRequested += (_, _) => { Interlocked.Increment(ref requests); received.Set(); };
        Assert.True(service.TryAcquire());

        Send(service.PipeName, System.Text.Encoding.ASCII.GetBytes(message));
        NotifyUntil(received, SecondInstanceAction.BringToFront);

        Assert.Equal(1, Volatile.Read(ref requests));
    }

    [Fact]
    public void OversizedMessage_IsCappedAndIgnored()
    {
        using var service = new SingleInstanceService(_suffix);
        var requests = 0;
        using var received = new ManualResetEventSlim();
        service.SecondInstanceRequested += (_, _) => { Interlocked.Increment(ref requests); received.Set(); };
        Assert.True(service.TryAcquire());

        // No line break: the unbounded ReadLine used to buffer all of it.
        Send(service.PipeName, new byte[64 * 1024]);
        NotifyUntil(received, SecondInstanceAction.OpenWindow);

        Assert.Equal(1, Volatile.Read(ref requests));
    }

    private static NamedPipeServerStream CreateServer(string pipeName) =>
        new(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static void Send(string pipeName, byte[] payload)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
        client.Connect(5000);
        try
        {
            client.Write(payload);
        }
        catch (IOException)
        {
            // Expected for the oversized payload: the listener hangs up once its cap is reached.
        }
    }

    /// <summary>The listener may be between connections (or backing off); retry the notification
    /// until it lands, failing the test if it never does.</summary>
    private void NotifyUntil(ManualResetEventSlim received, SecondInstanceAction action)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            SingleInstanceService.NotifyRunningInstance(action, _suffix);
            if (received.Wait(TimeSpan.FromMilliseconds(300))) return;
        }

        Assert.Fail("The running instance never received the notification.");
    }
}

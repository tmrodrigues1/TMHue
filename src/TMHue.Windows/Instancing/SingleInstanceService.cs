using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace TMHue.App.Infrastructure;

public enum SecondInstanceAction
{
    OpenWindow,
    BringToFront,
    StartCapture
}

/// <summary>
/// Guarantees a single running instance via a named Mutex. A second launch relays its
/// requested action to the first instance over a named pipe, then exits immediately.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private static readonly string CurrentUserSid = GetCurrentUserSid();
    private static readonly string MutexName = $@"Local\TMHue.SingleInstance.{CurrentUserSid}";
    private static readonly string PipeName = $"TMHue.IPC.{CurrentUserSid}";

    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;
    private int _disposed;

    public event EventHandler<SecondInstanceAction>? SecondInstanceRequested;

    /// <summary>Attempts to acquire ownership. Returns false when another instance already owns it.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            StartListening();
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void NotifyRunningInstance(SecondInstanceAction action)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(action.ToString());
        }
        catch
        {
            // The first instance may be shutting down; nothing meaningful to recover here.
        }
    }

    private static string GetCurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value
            ?? throw new InvalidOperationException("The current Windows user does not have a security identifier.");
    }

    private void StartListening()
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var line = await reader.ReadLineAsync(token);
                    if (Enum.TryParse<SecondInstanceAction>(line, out var action))
                        SecondInstanceRequested?.Invoke(this, action);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Keep the listener alive across transient pipe errors.
                }
            }
        }, token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        var listenerCts = Interlocked.Exchange(ref _listenerCts, null);
        listenerCts?.Cancel();
        listenerCts?.Dispose();

        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null) return;

        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}

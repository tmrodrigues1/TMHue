using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace TMHue.Windows.Instancing;

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
    // The longest valid message is an action name plus a line break; anything bigger is noise.
    private const int MaxMessageBytes = 64;
    private static readonly TimeSpan MessageReadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private static readonly string CurrentUserSid = GetCurrentUserSid();

    private readonly string _mutexName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;
    private int _listenerAttempts;
    private int _disposed;

    public SingleInstanceService() : this(string.Empty) { }

    /// <summary>Isolated names for tests, so they never collide with a running TMHue.</summary>
    internal SingleInstanceService(string nameSuffix)
    {
        _mutexName = $@"Local\TMHue.SingleInstance.{CurrentUserSid}{nameSuffix}";
        PipeName = BuildPipeName(nameSuffix);
    }

    public event EventHandler<SecondInstanceAction>? SecondInstanceRequested;

    internal string PipeName { get; }

    /// <summary>How many times the listener tried to create its pipe server.</summary>
    internal int ListenerAttempts => Volatile.Read(ref _listenerAttempts);

    /// <summary>Attempts to acquire ownership. Returns false when another instance already owns it.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            StartListening();
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void NotifyRunningInstance(SecondInstanceAction action) =>
        NotifyRunningInstance(action, string.Empty);

    internal static void NotifyRunningInstance(SecondInstanceAction action, string nameSuffix)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", BuildPipeName(nameSuffix), PipeDirection.Out,
                PipeOptions.CurrentUserOnly);
            client.Connect(500);
            client.Write(Encoding.ASCII.GetBytes(action + "\n"));
        }
        catch
        {
            // The first instance may be shutting down; nothing meaningful to recover here.
        }
    }

    // The pipe namespace is machine-wide while the Mutex is per session ("Local\"); the
    // session id keeps the same user's sessions from fighting over one pipe.
    private static string BuildPipeName(string nameSuffix)
    {
        using var process = Process.GetCurrentProcess();
        return $"TMHue.IPC.{CurrentUserSid}.{process.SessionId}{nameSuffix}";
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
        Task.Run(() => ListenAsync(token), token);
    }

    private async Task ListenAsync(CancellationToken token)
    {
        var retryDelay = InitialRetryDelay;
        while (!token.IsCancellationRequested)
        {
            try
            {
                Interlocked.Increment(ref _listenerAttempts);
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                retryDelay = InitialRetryDelay;

                if (await ReadActionAsync(server, token).ConfigureAwait(false) is { } action)
                    SecondInstanceRequested?.Invoke(this, action);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep the listener alive across pipe errors, but never spin: when another
                // process holds the pipe name, creating the server fails immediately every time.
                try
                {
                    await Task.Delay(retryDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retryDelay = retryDelay * 2 < MaxRetryDelay ? retryDelay * 2 : MaxRetryDelay;
            }
        }
    }

    /// <summary>Reads one short line. A client that stalls or floods is dropped by the timeout
    /// and the size cap instead of holding the listener or growing a buffer.</summary>
    private static async Task<SecondInstanceAction?> ReadActionAsync(Stream stream, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(MessageReadTimeout);

        var buffer = new byte[MaxMessageBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token).ConfigureAwait(false);
            if (read == 0) break;
            length += read;
            if (Array.IndexOf(buffer, (byte)'\n', 0, length) >= 0) break;
        }

        var line = Encoding.UTF8.GetString(buffer, 0, length).Split('\n')[0].Trim('﻿', '\r', ' ');
        return Enum.TryParse<SecondInstanceAction>(line, out var action) && Enum.IsDefined(action)
            ? action
            : null;
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

using System.IO.Pipes;
using System.Text;

namespace Cascade.UI;

/// <summary>
/// Enforces single-instance behaviour for an application and routes a second
/// launch's command-line arguments to the already-running (primary) instance.
///
/// <para>
/// The first process to call <see cref="TryAcquireOwnership"/> becomes the
/// primary and owns a named <see cref="Mutex"/>. It then calls
/// <see cref="StartListening"/> to serve a named pipe. Any subsequent process
/// fails to acquire ownership and instead calls <see cref="SendToPrimary"/> to
/// forward its arguments over that pipe, then exits without creating a window.
/// </para>
///
/// <para>
/// Windows-only for now (named pipes + a global mutex). The names are scoped by
/// an application identifier so distinct apps never collide.
/// </para>
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    // Arguments are newline-delimited UTF-8. File paths (the intended payload)
    // cannot contain newline characters on Windows, so this framing is lossless.
    private const char ArgDelimiter = '\n';

    private readonly string mutexName;
    private readonly string pipeName;
    private readonly CancellationTokenSource cts = new();

    private Mutex? mutex;
    private bool ownsMutex;
    private Task? listenerTask;
    private bool disposed;

    /// <summary>
    /// Creates a guard scoped to the given application identifier.
    /// </summary>
    /// <param name="appId">
    /// A stable machine-readable identifier for the application (e.g. the entry
    /// assembly name). Determines which processes are considered "the same app".
    /// </param>
    public SingleInstanceGuard(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        mutexName = $"Cascade-SingleInstance-{appId}";
        pipeName = $"Cascade-SingleInstance-{appId}-args";
    }

    /// <summary>
    /// Attempts to become the primary instance. Returns true if this process now
    /// owns the single-instance mutex (it is the primary); false if another
    /// instance already holds it (this process is a secondary and should forward
    /// its arguments via <see cref="SendToPrimary"/> then exit).
    /// </summary>
    public bool TryAcquireOwnership()
    {
        mutex = new Mutex(initiallyOwned: false, mutexName);
        try
        {
            // Zero-timeout wait: acquire immediately if free, otherwise report contention.
            ownsMutex = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // A previous primary crashed without releasing. WaitOne still transfers
            // ownership to us in this case, so we are the new primary.
            ownsMutex = true;
        }

        return ownsMutex;
    }

    /// <summary>
    /// Starts serving the argument-forwarding pipe. Call only on the primary.
    /// Each time a secondary connects, <paramref name="onArgumentsReceived"/> is
    /// invoked with the forwarded arguments. The callback runs on a background
    /// thread — marshal to the UI thread inside it if needed.
    /// </summary>
    public void StartListening(Action<string[]> onArgumentsReceived)
    {
        ArgumentNullException.ThrowIfNull(onArgumentsReceived);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (listenerTask is not null)
        {
            return;
        }

        listenerTask = Task.Run(() => ListenLoop(onArgumentsReceived, cts.Token));
    }

    /// <summary>
    /// Forwards this (secondary) process's arguments to the running primary.
    /// Returns true if the arguments were delivered. Call only after
    /// <see cref="TryAcquireOwnership"/> returned false.
    /// </summary>
    public bool SendToPrimary(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);

            // The primary may be mid-startup; give it a brief window to appear.
            client.Connect(2000);

            string payload = string.Join(ArgDelimiter, args);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // Primary didn't answer (still launching, or exited between the mutex
            // check and now). Nothing more we can do; the secondary simply exits.
            return false;
        }
    }

    private async Task ListenLoop(Action<string[]> onArgumentsReceived, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string payload = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

                string[] args = payload.Length == 0
                    ? []
                    : payload.Split(ArgDelimiter);

                onArgumentsReceived(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Broken connection; loop and serve the next client.
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        cts.Cancel();
        try
        {
            listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Listener faulted or was cancelled during teardown — nothing to salvage.
        }

        if (mutex is not null)
        {
            if (ownsMutex)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Not owned on this thread (defensive) — the handle close still frees it.
                }
            }

            mutex.Dispose();
            mutex = null;
        }

        cts.Dispose();
    }
}

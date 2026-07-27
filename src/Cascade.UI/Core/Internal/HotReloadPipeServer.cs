using System.IO.Pipes;

namespace Cascade.UI.Core.Internal;

/// <summary>
/// Named pipe server that listens for hot reload deltas from the Cascade CLI.
/// Runs inside the application process. The CLI's watch command sends compiled
/// deltas (metadata + IL + PDB bytes) over pipe "cascade-hotreload-{pid}".
/// </summary>
internal sealed class HotReloadPipeServer : IDisposable
{
    private readonly HotReloadEngine engine;
    private readonly string pipeName;
    private readonly CancellationTokenSource cts = new();
    private Task? listenerTask;
    private bool disposed;

    public HotReloadPipeServer(HotReloadEngine engine)
    {
        this.engine = engine;
        pipeName = $"cascade-hotreload-{Environment.ProcessId}";
    }

    /// <summary>The pipe name clients should connect to.</summary>
    public string PipeName => pipeName;

    /// <summary>Whether the server is currently listening.</summary>
    public bool IsListening => listenerTask is not null && !listenerTask.IsCompleted;

    /// <summary>Starts listening for delta packages on the named pipe.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (listenerTask is not null)
        {
            return;
        }

        listenerTask = Task.Run(() => ListenLoop(cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                try
                {
                    await HandleConnection(pipe, ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Connection error — continue listening
                }
                finally
                {
                    if (pipe.IsConnected)
                    {
                        pipe.Disconnect();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Pipe creation error — brief pause then retry
                await Task.Delay(100, ct);
            }
        }
    }

    /// <summary>
    /// Handles a single connection. Protocol:
    ///   Request:  [command:1][payload]
    ///   Commands: 0x01 = ApplyDelta, 0x02 = Ping
    ///   ApplyDelta payload:
    ///     [changedFileLen:4][changedFile:N]
    ///     [metadataLen:4][metadataBytes:N]
    ///     [ilLen:4][ilBytes:N]
    ///     [pdbLen:4][pdbBytes:N]
    ///     [updatedTypeCount:4]([typeLen:4][typeName:N])*
    ///     [requiresRestart:1]
    ///   Response: [status:1] (0=success, 1=failed, 2=restart_required)
    /// </summary>
    private async Task HandleConnection(NamedPipeServerStream pipe, CancellationToken ct)
    {
        int command = pipe.ReadByte();
        if (command == -1)
        {
            return;
        }

        if (command == 0x02) // Ping
        {
            pipe.WriteByte(0x00); // Pong
            await pipe.FlushAsync(ct);
            return;
        }

        if (command != 0x01) // ApplyDelta
        {
            pipe.WriteByte(0x01); // Unknown command → failed
            await pipe.FlushAsync(ct);
            return;
        }

        // Read the delta package
        string changedFile = await ReadStringAsync(pipe, ct);
        byte[] metadataBytes = await ReadBytesAsync(pipe, ct);
        byte[] ilDelta = await ReadBytesAsync(pipe, ct);
        byte[] pdbDelta = await ReadBytesAsync(pipe, ct);

        int typeCount = await ReadInt32Async(pipe, ct);
        var updatedTypes = new string[typeCount];
        for (int i = 0; i < typeCount; i++)
        {
            updatedTypes[i] = await ReadStringAsync(pipe, ct);
        }

        int restartByte = pipe.ReadByte();
        bool requiresRestart = restartByte == 1;

        var delta = new MetadataDelta
        {
            ChangedFile = changedFile,
            MetadataBytes = metadataBytes,
            IlDelta = ilDelta,
            PdbDelta = pdbDelta,
            UpdatedTypes = updatedTypes,
            RequiresRestart = requiresRestart,
        };

        var result = engine.ApplyDelta(delta);

        byte status = result.Status switch
        {
            HotReloadStatus.Success => 0x00,
            HotReloadStatus.RestartRequired => 0x02,
            _ => 0x01,
        };

        pipe.WriteByte(status);
        await pipe.FlushAsync(ct);
    }

    private static async Task<string> ReadStringAsync(Stream stream, CancellationToken ct)
    {
        int length = await ReadInt32Async(stream, ct);
        if (length == 0)
        {
            return "";
        }

        byte[] buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, ct);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    private static async Task<byte[]> ReadBytesAsync(Stream stream, CancellationToken ct)
    {
        int length = await ReadInt32Async(stream, ct);
        if (length == 0)
        {
            return [];
        }

        byte[] buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, ct);
        return buffer;
    }

    private static async Task<int> ReadInt32Async(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer, ct);
        return BitConverter.ToInt32(buffer, 0);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            cts.Cancel();
            cts.Dispose();
        }
    }
}

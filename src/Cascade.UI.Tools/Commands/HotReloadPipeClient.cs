using System.IO.Pipes;
using System.Text;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Named pipe client that sends hot reload deltas to a running Cascade application.
/// Connects to the pipe "cascade-hotreload-{pid}" opened by HotReloadPipeServer.
/// </summary>
internal sealed class HotReloadPipeClient : IDisposable
{
    private readonly string pipeName;
    private readonly int timeoutMs;
    private bool disposed;

    public HotReloadPipeClient(int targetPid, int timeoutMs = 5000)
    {
        pipeName = $"cascade-hotreload-{targetPid}";
        this.timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Sends a compiled delta to the running application.
    /// Returns the status: 0=success, 1=failed, 2=restart_required, -1=connection error.
    /// </summary>
    public int SendDelta(DeltaResult delta, string changedFile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMs);

            // Command: 0x01 = ApplyDelta
            pipe.WriteByte(0x01);

            // Changed file
            WriteString(pipe, changedFile);

            // Metadata, IL, PDB bytes
            WriteBytes(pipe, delta.MetadataBytes);
            WriteBytes(pipe, delta.IlDelta);
            WriteBytes(pipe, delta.PdbDelta);

            // Updated types
            WriteInt32(pipe, delta.UpdatedTypes.Length);
            foreach (string typeName in delta.UpdatedTypes)
            {
                WriteString(pipe, typeName);
            }

            // RequiresRestart flag
            pipe.WriteByte(0); // deltas that get here are never restart-required

            pipe.Flush();

            // Read response
            int status = pipe.ReadByte();
            return status == -1 ? -1 : status;
        }
        catch (TimeoutException)
        {
            return -1;
        }
        catch (IOException)
        {
            return -1;
        }
    }

    /// <summary>
    /// Pings the running application to verify the pipe is alive.
    /// Returns true if the app responded.
    /// </summary>
    public bool Ping()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMs);
            pipe.WriteByte(0x02); // Ping command
            pipe.Flush();
            int response = pipe.ReadByte();
            return response == 0x00; // Pong
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteBytes(Stream stream, byte[] bytes)
    {
        WriteInt32(stream, bytes.Length);
        if (bytes.Length > 0)
        {
            stream.Write(bytes);
        }
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    public void Dispose()
    {
        disposed = true;
    }
}

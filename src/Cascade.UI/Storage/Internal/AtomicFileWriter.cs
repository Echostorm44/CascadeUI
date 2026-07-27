namespace Cascade.UI;

/// <summary>
/// Crash-safe file writer. Writes to a temporary file in the same directory,
/// then renames over the target atomically. If <see cref="Commit"/> is not
/// called before disposal, the temporary file is deleted.
/// </summary>
internal sealed class AtomicFileWriter : IDisposable
{
    private readonly string targetPath;
    private readonly string tempPath;
    private FileStream? stream;
    private bool committed;

    internal AtomicFileWriter(string targetPath)
    {
        this.targetPath = targetPath;
        tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N")[..8];

        var directory = System.IO.Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    /// <summary>
    /// The writable stream. Throws if the writer has been disposed or committed.
    /// </summary>
    internal Stream Stream
    {
        get
        {
            ObjectDisposedException.ThrowIf(stream is null, this);
            return stream;
        }
    }

    /// <summary>
    /// Flushes the stream to disk and atomically replaces the target file.
    /// </summary>
    internal void Commit()
    {
        ObjectDisposedException.ThrowIf(stream is null, this);

        stream.Flush(flushToDisk: true);
        stream.Dispose();
        stream = null;
        File.Move(tempPath, targetPath, overwrite: true);
        committed = true;
    }

    public void Dispose()
    {
        stream?.Dispose();
        stream = null;

        if (!committed)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup of the temp file
            }
        }
    }
}

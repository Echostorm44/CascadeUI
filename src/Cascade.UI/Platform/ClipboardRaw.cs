namespace Cascade.UI;

/// <summary>One clipboard format exactly as the source app offered it.</summary>
/// <param name="Id">The format id (registered ids, 0xC000 and up, are only stable within a session).</param>
/// <param name="Name">The format name ("HTML Format", "PNG", "CF_UNICODETEXT", …).</param>
/// <param name="Data">The format's bytes (for CF_ENHMETAFILE, the metafile bits).</param>
public sealed record ClipboardRawFormat(uint Id, string Name, ReadOnlyMemory<byte> Data);

/// <summary>
/// Every format of a clipboard content, byte for byte — what a clipboard manager stores so that
/// pasting later gives the target app exactly what was copied (RTF stays RTF, images keep their
/// original bytes). Capture with <see cref="Clipboard.CaptureRaw"/>, restore with
/// <see cref="Clipboard.WriteRaw"/>.
/// </summary>
public sealed class ClipboardRawSnapshot
{
    /// <summary>Creates a snapshot from formats (e.g. ones loaded back from storage).</summary>
    public ClipboardRawSnapshot(IReadOnlyList<ClipboardRawFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        Formats = formats;
        foreach (var format in formats)
        {
            TotalBytes += format.Data.Length;
        }
    }

    /// <summary>The formats in the order the source offered them.</summary>
    public IReadOnlyList<ClipboardRawFormat> Formats { get; }

    /// <summary>Total size of all format data.</summary>
    public long TotalBytes { get; }

    /// <summary>The format with this name, or null.</summary>
    public ClipboardRawFormat? Find(string name)
    {
        foreach (var format in Formats)
        {
            if (string.Equals(format.Name, name, StringComparison.Ordinal))
            {
                return format;
            }
        }
        return null;
    }
}

/// <summary>The app that put content on the clipboard.</summary>
public sealed class ClipboardSourceApp
{
    private readonly Lazy<string?> path;

    internal ClipboardSourceApp(uint processId)
    {
        ProcessId = processId;
        path = new Lazy<string?>(() => OperatingSystem.IsWindows() ? Win32Clipboard.GetProcessPath(processId) : null);
    }

    /// <summary>The source process id.</summary>
    public uint ProcessId { get; }

    /// <summary>
    /// The source executable's full path (resolved on first access; null when the process has exited
    /// or cannot be queried).
    /// </summary>
    public string? ExecutablePath => path.Value;
}

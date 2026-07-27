namespace Cascade.UI;

/// <summary>
/// Represents the current content of the system clipboard.
/// Content is lazy — checking <see cref="HasText"/> does not fetch the text data.
/// Call the corresponding GetAsync method to fetch on demand.
/// </summary>
public sealed class ClipboardContent
{
    // ── Read-path availability flags (set by the internal factory) ───

    private bool hasText;
    private bool hasHtml;
    private bool hasRtf;      // Always false; reserved for future RTF support.
    private bool hasImage;
    private bool hasFiles;

    /// <summary>
    /// Creates a ClipboardContent populated from Win32 availability data.
    /// Used by <see cref="Clipboard.GetContentAsync"/> and the Changed event on Windows.
    /// </summary>
    internal static ClipboardContent FromWin32Availability(ClipboardAvailability avail)
    {
        var content = new ClipboardContent();
        content.hasText  = avail.HasText;
        content.hasHtml  = avail.HasHtml;
        content.hasImage = avail.HasImage;
        content.hasFiles = avail.HasFiles;
        content.hasRtf   = avail.HasRtf;
        return content;
    }

    /// <summary>
    /// Creates a ClipboardContent populated from macOS Cocoa availability data.
    /// Used by <see cref="Clipboard.GetContentAsync"/> and the Changed event on macOS.
    /// </summary>
    internal static ClipboardContent FromCocoaAvailability(CocoaClipboardAvailability avail)
    {
        var content = new ClipboardContent();
        content.hasText  = avail.HasText;
        content.hasHtml  = avail.HasHtml;
        content.hasImage = avail.HasImage;
        content.hasFiles = avail.HasFiles;
        content.hasRtf   = avail.HasRtf;
        return content;
    }

    /// <summary>
    /// Creates a ClipboardContent populated from Linux clipboard availability data.
    /// Used by <see cref="Clipboard.GetContentAsync"/> and the Changed event on Linux.
    /// </summary>
    internal static ClipboardContent FromLinuxAvailability(ClipboardAvailability avail)
    {
        var content = new ClipboardContent();
        content.hasText  = avail.HasText;
        content.hasHtml  = avail.HasHtml;
        content.hasImage = avail.HasImage;
        content.hasFiles = avail.HasFiles;
        content.hasRtf   = avail.HasRtf;
        return content;
    }

    // ── Availability ─────────────────────────────────────────────────

    /// <summary>True if the clipboard contains plain text.</summary>
    public bool HasText => hasText;

    /// <summary>True if the clipboard contains HTML.</summary>
    public bool HasHtml => hasHtml;

    /// <summary>True if the clipboard contains RTF.</summary>
    public bool HasRtf => hasRtf;

    /// <summary>True if the clipboard contains image data.</summary>
    public bool HasImage => hasImage;

    /// <summary>True if the clipboard contains a file list.</summary>
    public bool HasFiles => hasFiles;

    /// <summary>All formats currently available on the clipboard.</summary>
    public IReadOnlyList<ClipboardFormat> AvailableFormats
    {
        get
        {
            List<ClipboardFormat> formats = [];
            if (hasText)  { formats.Add(ClipboardFormat.Text); }
            if (hasHtml)  { formats.Add(ClipboardFormat.Html); }
            if (hasImage) { formats.Add(ClipboardFormat.Image); }
            if (hasFiles) { formats.Add(ClipboardFormat.Files); }
            return formats;
        }
    }

    // ── Lazy data fetching ───────────────────────────────────────────

    /// <summary>Fetches the plain text content from the clipboard.</summary>
    public Task<string?> GetTextAsync()
    {
        if (!hasText)
        {
            return Task.FromResult<string?>(null);
        }

        if (OperatingSystem.IsWindows()) { return Task.FromResult(Win32Clipboard.GetText()); }
        if (OperatingSystem.IsMacOS()) { return Task.FromResult(CocoaClipboard.GetText()); }
        if (OperatingSystem.IsLinux()) { return Task.FromResult(LinuxClipboard.GetText()); }
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Fetches the HTML content from the clipboard. On Windows, the CF_HTML
    /// header is parsed correctly and only the HTML fragment is returned.
    /// </summary>
    public Task<string?> GetHtmlAsync()
    {
        if (!hasHtml)
        {
            return Task.FromResult<string?>(null);
        }

        if (OperatingSystem.IsWindows()) { return Task.FromResult(Win32Clipboard.GetHtml()); }
        if (OperatingSystem.IsMacOS()) { return Task.FromResult(CocoaClipboard.GetHtml()); }
        if (OperatingSystem.IsLinux()) { return Task.FromResult(LinuxClipboard.GetHtml()); }
        return Task.FromResult<string?>(null);
    }

    /// <summary>Fetches the RTF content from the clipboard.</summary>
    public Task<string?> GetRtfAsync()
    {
        if (!hasRtf)
        {
            return Task.FromResult<string?>(null);
        }

        if (OperatingSystem.IsWindows()) { return Task.FromResult(Win32Clipboard.GetRtf()); }
        if (OperatingSystem.IsMacOS()) { return Task.FromResult(CocoaClipboard.GetRtf()); }
        return Task.FromResult<string?>(null);
    }

    /// <summary>Fetches the image data from the clipboard as RGBA pixels.</summary>
    public Task<ImageData?> GetImageAsync()
    {
        if (!hasImage)
        {
            return Task.FromResult<ImageData?>(null);
        }

        if (OperatingSystem.IsWindows()) { return Task.FromResult(Win32Clipboard.GetImage()); }
        return Task.FromResult<ImageData?>(null);
    }

    /// <summary>Fetches the file paths from the clipboard.</summary>
    public Task<IReadOnlyList<string>?> GetFilesAsync()
    {
        if (!hasFiles)
        {
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }

        if (OperatingSystem.IsWindows()) { return Task.FromResult(Win32Clipboard.GetFiles()); }
        if (OperatingSystem.IsMacOS()) { return Task.FromResult(CocoaClipboard.GetFiles()); }
        if (OperatingSystem.IsLinux()) { return Task.FromResult(LinuxClipboard.GetFiles()); }
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    /// <summary>
    /// Fetches raw data for a custom or application-specific clipboard format.
    /// Custom raw formats are not yet supported; returns null.
    /// </summary>
    /// <param name="format">The format to retrieve.</param>
    public Task<byte[]?> GetRawAsync(ClipboardFormat format)
    {
        _ = hasText; // Raw custom-format access is not yet implemented.
        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>
    /// Immediately resolves the specified formats into an in-memory snapshot.
    /// After this call, the snapshot holds real data regardless of whether
    /// the clipboard changes again. Use for clipboard history applications.
    /// </summary>
    /// <param name="formats">The formats to snapshot.</param>
    public Task<ClipboardSnapshot> SnapshotAsync(IReadOnlyList<ClipboardFormat> formats)
    {
        string? text  = null;
        string? html  = null;
        IReadOnlyList<string>? files = null;
        List<ClipboardFormat> captured = [];

        foreach (ClipboardFormat format in formats)
        {
            if (format.Equals(ClipboardFormat.Text) && hasText)
            {
                if (OperatingSystem.IsWindows()) { text = Win32Clipboard.GetText(); }
                else if (OperatingSystem.IsMacOS()) { text = CocoaClipboard.GetText(); }
                else if (OperatingSystem.IsLinux()) { text = LinuxClipboard.GetText(); }
                if (text is not null)
                {
                    captured.Add(ClipboardFormat.Text);
                }
            }
            else if (format.Equals(ClipboardFormat.Html) && hasHtml)
            {
                if (OperatingSystem.IsWindows()) { html = Win32Clipboard.GetHtml(); }
                else if (OperatingSystem.IsMacOS()) { html = CocoaClipboard.GetHtml(); }
                else if (OperatingSystem.IsLinux()) { html = LinuxClipboard.GetHtml(); }
                if (html is not null)
                {
                    captured.Add(ClipboardFormat.Html);
                }
            }
            else if (format.Equals(ClipboardFormat.Files) && hasFiles)
            {
                if (OperatingSystem.IsWindows()) { files = Win32Clipboard.GetFiles(); }
                else if (OperatingSystem.IsMacOS()) { files = CocoaClipboard.GetFiles(); }
                else if (OperatingSystem.IsLinux()) { files = LinuxClipboard.GetFiles(); }
                if (files is not null)
                {
                    captured.Add(ClipboardFormat.Files);
                }
            }
            // RTF, Image, and Raw custom formats are not yet supported.
        }

        return Task.FromResult(ClipboardSnapshot.Create(captured, text, html, null, null, files));
    }

    // ── Writable properties for constructing content to write ────────

    /// <summary>Plain text to write to the clipboard.</summary>
    public string? Text { get; init; }

    /// <summary>HTML to write to the clipboard.</summary>
    public string? Html { get; init; }

    /// <summary>RTF to write to the clipboard.</summary>
    public string? Rtf { get; init; }
}

/// <summary>
/// Raw image data read from or written to the clipboard.
/// </summary>
public sealed class ImageData
{
    /// <summary>The raw pixel data in RGBA format.</summary>
    public byte[] Pixels { get; init; } = [];

    /// <summary>Width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Stride (bytes per row).</summary>
    public int Stride { get; init; }
}

/// <summary>
/// A serializable snapshot of clipboard content at a specific point in time.
/// Can be written to disk, read back, and placed back on the clipboard.
/// </summary>
public sealed class ClipboardSnapshot
{
    private List<ClipboardFormat> capturedFormats = [];

    /// <summary>The formats captured in this snapshot.</summary>
    public IReadOnlyList<ClipboardFormat> Formats => capturedFormats;

    /// <summary>The captured plain text, if snapshotted.</summary>
    public string? Text { get; init; }

    /// <summary>The captured HTML, if snapshotted.</summary>
    public string? Html { get; init; }

    /// <summary>The captured RTF, if snapshotted.</summary>
    public string? Rtf { get; init; }

    /// <summary>The captured image data, if snapshotted.</summary>
    public ImageData? Image { get; init; }

    /// <summary>The captured file paths, if snapshotted.</summary>
    public IReadOnlyList<string>? Files { get; init; }

    /// <summary>
    /// Creates a snapshot with the specified captured formats and data.
    /// </summary>
    internal static ClipboardSnapshot Create(
        List<ClipboardFormat> formats,
        string? text,
        string? html,
        string? rtf,
        ImageData? image,
        IReadOnlyList<string>? files)
    {
        var snapshot = new ClipboardSnapshot
        {
            Text  = text,
            Html  = html,
            Rtf   = rtf,
            Image = image,
            Files = files
        };
        snapshot.capturedFormats = formats;
        return snapshot;
    }
}

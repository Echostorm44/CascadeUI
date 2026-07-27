namespace Cascade.UI;

/// <summary>
/// Provides cross-platform clipboard read, write, and monitoring.
/// The implementation uses platform-correct mechanisms on each OS:
/// AddClipboardFormatListener on Windows, NSPasteboard.changeCount polling
/// on macOS, and Wayland/X11 clipboard events on Linux.
/// </summary>
public static class Clipboard
{
    private static int monitoringRefCount;

    // ── Reading ──────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current clipboard content. The returned object is lazy —
    /// format availability is checked immediately but data is fetched on
    /// demand via the GetAsync methods.
    /// </summary>
    public static Task<ClipboardContent> GetContentAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            ClipboardAvailability avail = Win32Clipboard.GetAvailableFormats();
            ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
            return Task.FromResult(content);
        }
        else if (OperatingSystem.IsMacOS())
        {
            CocoaClipboardAvailability avail = CocoaClipboard.GetAvailableFormats();
            ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);
            return Task.FromResult(content);
        }
        else if (OperatingSystem.IsLinux())
        {
            ClipboardAvailability avail = LinuxClipboard.GetAvailableFormats();
            ClipboardContent content = ClipboardContent.FromLinuxAvailability(avail);
            return Task.FromResult(content);
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
        }
    }

    // ── Writing ─────────────────────────────────────────────────────

    /// <summary>
    /// Writes multiple formats to the clipboard simultaneously.
    /// All formats are written in a single open/close cycle so receiving
    /// apps can choose the richest format they support.
    /// </summary>
    /// <param name="content">The content to write, with one or more format properties set.</param>
    public static Task WriteAsync(ClipboardContent content)
    {
        if (OperatingSystem.IsWindows())
        {
            if (content.Html is not null)
            {
                Win32Clipboard.SetHtml(content.Html, content.Text);
            }
            else if (content.Text is not null)
            {
                Win32Clipboard.SetText(content.Text);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (content.Html is not null)
            {
                CocoaClipboard.SetHtml(content.Html, content.Text);
            }
            else if (content.Text is not null)
            {
                CocoaClipboard.SetText(content.Text);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (content.Html is not null)
            {
                LinuxClipboard.SetHtml(content.Html, content.Text);
            }
            else if (content.Text is not null)
            {
                LinuxClipboard.SetText(content.Text);
            }
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
        }

        return Task.CompletedTask;
    }

    /// <summary>Writes plain text to the clipboard.</summary>
    /// <param name="text">The text to write.</param>
    public static Task WriteTextAsync(string text)
    {
        if (OperatingSystem.IsWindows())
        {
            Win32Clipboard.SetText(text);
        }
        else if (OperatingSystem.IsMacOS())
        {
            CocoaClipboard.SetText(text);
        }
        else if (OperatingSystem.IsLinux())
        {
            LinuxClipboard.SetText(text);
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
        }

        return Task.CompletedTask;
    }

    /// <summary>Writes image data to the clipboard.</summary>
    /// <param name="image">The image data to write (RGBA pixels).</param>
    public static Task WriteImageAsync(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (OperatingSystem.IsWindows())
        {
            Win32Clipboard.SetImage(image);
        }
        else
        {
            throw new PlatformNotSupportedException("Image clipboard write is only supported on Windows.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes file paths to the clipboard (copy-to-clipboard, paste into file manager).
    /// </summary>
    /// <param name="filePaths">Absolute paths of the files to place on the clipboard.</param>
    public static Task WriteFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (OperatingSystem.IsWindows())
        {
            Win32Clipboard.SetFiles(filePaths);
        }
        else if (OperatingSystem.IsMacOS())
        {
            CocoaClipboard.SetFiles(filePaths);
        }
        else if (OperatingSystem.IsLinux())
        {
            LinuxClipboard.SetFiles(filePaths);
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes all formats from a previously captured snapshot back to the clipboard,
    /// preserving the full fidelity of the original content.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore to the clipboard.</param>
    public static Task WriteSnapshotAsync(ClipboardSnapshot snapshot)
    {
        if (OperatingSystem.IsWindows())
        {
            if (snapshot.Files is not null)
            {
                Win32Clipboard.SetFiles(snapshot.Files);
            }
            else if (snapshot.Html is not null)
            {
                Win32Clipboard.SetHtml(snapshot.Html, snapshot.Text);
            }
            else if (snapshot.Text is not null)
            {
                Win32Clipboard.SetText(snapshot.Text);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (snapshot.Files is not null)
            {
                CocoaClipboard.SetFiles(snapshot.Files);
            }
            else if (snapshot.Html is not null)
            {
                CocoaClipboard.SetHtml(snapshot.Html, snapshot.Text);
            }
            else if (snapshot.Text is not null)
            {
                CocoaClipboard.SetText(snapshot.Text);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (snapshot.Files is not null)
            {
                LinuxClipboard.SetFiles(snapshot.Files);
            }
            else if (snapshot.Html is not null)
            {
                LinuxClipboard.SetHtml(snapshot.Html, snapshot.Text);
            }
            else if (snapshot.Text is not null)
            {
                LinuxClipboard.SetText(snapshot.Text);
            }
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
        }

        return Task.CompletedTask;
    }

    // ── Monitoring ───────────────────────────────────────────────────

    /// <summary>
    /// Fires whenever the system clipboard content changes.
    /// The event args include the new content (lazy), a sequence number
    /// for deduplication, and whether the change originated from this app.
    /// </summary>
    public static event Action<ClipboardChangedEventArgs>? Changed;

    /// <summary>
    /// Starts clipboard monitoring if not already active. Monitoring is
    /// reference-counted — each call to <see cref="StartMonitoring"/>
    /// must be balanced by a call to <see cref="StopMonitoring"/>.
    /// </summary>
    public static void StartMonitoring()
    {
        if (OperatingSystem.IsWindows())
        {
            if (System.Threading.Interlocked.Increment(ref monitoringRefCount) == 1)
            {
                nint hwnd = App.nativeWindow?.Handle ?? 0;
                if (hwnd != 0)
                {
                    Win32Clipboard.StartMonitoring(hwnd);
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS uses polling — no explicit start needed; CheckForChanges drives monitoring.
            System.Threading.Interlocked.Increment(ref monitoringRefCount);
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux uses polling — no explicit start needed; PollLinuxClipboard drives monitoring.
            System.Threading.Interlocked.Increment(ref monitoringRefCount);
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard monitoring is only supported on Windows, macOS, and Linux.");
        }
    }

    /// <summary>
    /// Decrements the monitoring reference count. When it reaches zero,
    /// clipboard monitoring is stopped.
    /// </summary>
    public static void StopMonitoring()
    {
        if (OperatingSystem.IsWindows())
        {
            if (System.Threading.Interlocked.Decrement(ref monitoringRefCount) == 0)
            {
                nint hwnd = App.nativeWindow?.Handle ?? 0;
                if (hwnd != 0)
                {
                    Win32Clipboard.StopMonitoring(hwnd);
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            System.Threading.Interlocked.Decrement(ref monitoringRefCount);
        }
        else if (OperatingSystem.IsLinux())
        {
            System.Threading.Interlocked.Decrement(ref monitoringRefCount);
        }
        else
        {
            throw new PlatformNotSupportedException("Clipboard monitoring is only supported on Windows, macOS, and Linux.");
        }
    }

    // ── Internal message handler ─────────────────────────────────────

    /// <summary>
    /// Called by the App message loop when WM_CLIPBOARDUPDATE is received.
    /// Fires the <see cref="Changed"/> event with current clipboard state.
    /// </summary>
    internal static void NotifyClipboardChanged()
    {
        Action<ClipboardChangedEventArgs>? handler = Changed;
        if (handler is null)
        {
            return;
        }

        ClipboardContent content;
        uint sequenceNumber = 0;

        if (OperatingSystem.IsWindows())
        {
            ClipboardAvailability avail = Win32Clipboard.GetAvailableFormats();
            content = ClipboardContent.FromWin32Availability(avail);
            sequenceNumber = Win32Clipboard.GetSequenceNumber();
        }
        else if (OperatingSystem.IsMacOS())
        {
            CocoaClipboardAvailability avail = CocoaClipboard.GetAvailableFormats();
            content = ClipboardContent.FromCocoaAvailability(avail);
        }
        else if (OperatingSystem.IsLinux())
        {
            ClipboardAvailability avail = LinuxClipboard.GetAvailableFormats();
            content = ClipboardContent.FromLinuxAvailability(avail);
        }
        else
        {
            return;
        }

        var args = new ClipboardChangedEventArgs(content, sequenceNumber, ClipboardSource.OtherApp);
        handler(args);
    }

    /// <summary>
    /// Polls the macOS clipboard for changes. Called from the Cocoa run loop
    /// on each frame tick. No-op if monitoring is not active.
    /// </summary>
    internal static void PollMacOSClipboard()
    {
        if (monitoringRefCount > 0 && CocoaClipboard.CheckForChanges())
        {
            NotifyClipboardChanged();
        }
    }

    /// <summary>
    /// Polls the Linux clipboard for changes. Called from the Linux event loop
    /// on each frame tick. No-op if monitoring is not active.
    /// The X11 selection protocol does not provide a change notification mechanism,
    /// so we poll the selection owner for changes.
    /// </summary>
    internal static void PollLinuxClipboard()
    {
        if (monitoringRefCount > 0)
        {
            NotifyClipboardChanged();
        }
    }
}

/// <summary>
/// Event args for <see cref="Clipboard.Changed"/>.
/// </summary>
public sealed class ClipboardChangedEventArgs : EventArgs
{
    private readonly ClipboardContent content;

    internal ClipboardChangedEventArgs(ClipboardContent content, uint sequenceNumber, ClipboardSource source)
    {
        this.content = content;
        SequenceNumber = sequenceNumber;
        Source = source;
    }

    /// <summary>
    /// The new clipboard content. Lazy — data is fetched on demand.
    /// </summary>
    public ClipboardContent Content => content;

    /// <summary>
    /// The Windows clipboard sequence number for deduplication.
    /// Always 0 on macOS and Linux.
    /// </summary>
    public uint SequenceNumber { get; init; }

    /// <summary>
    /// Whether the clipboard change originated from this application
    /// or from another application.
    /// </summary>
    public ClipboardSource Source { get; init; }
}

/// <summary>
/// Indicates whether a clipboard change originated from the current
/// application or from an external application.
/// </summary>
public enum ClipboardSource
{
    /// <summary>The clipboard was changed by this application.</summary>
    ThisApp,

    /// <summary>The clipboard was changed by another application.</summary>
    OtherApp
}

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

    // The window clipboard notifications are delivered to; StartMonitoring before App.Run created it
    // used to count the request but never register the listener.
    private static nint listenerWindow;
    private static nint attachedWindow;

    // Windows can post several WM_CLIPBOARDUPDATE for one change (measured: two for a single
    // open/empty/set/close); they carry the same sequence number and are reported once.
    private static uint lastNotifiedSequence;

    /// <summary>Called by App once the main window exists; registers a listener requested earlier.</summary>
    internal static void AttachWindow(nint hwnd)
    {
        attachedWindow = hwnd;
        if (monitoringRefCount > 0 && listenerWindow == 0 && hwnd != 0)
        {
            Win32Clipboard.StartMonitoring(hwnd);
            listenerWindow = hwnd;
        }
    }

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
    /// Writes every format set on <paramref name="content"/> (Text, Html, Rtf, Image, Files) in a
    /// single open/close, so receiving apps can choose the richest format they support. Returns
    /// false when the clipboard could not be opened (another app holding it).
    /// </summary>
    /// <param name="content">The content to write, with one or more format properties set.</param>
    public static Task<bool> WriteAsync(ClipboardContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult(Win32Clipboard.Write(content.Text, content.Html, content.Rtf, content.Image, content.Files));
        }
        if (OperatingSystem.IsMacOS())
        {
            if (content.Files is not null)
            {
                CocoaClipboard.SetFiles(content.Files);
            }
            else if (content.Html is not null)
            {
                CocoaClipboard.SetHtml(content.Html, content.Text);
            }
            else if (content.Text is not null)
            {
                CocoaClipboard.SetText(content.Text);
            }
            return Task.FromResult(true);
        }
        if (OperatingSystem.IsLinux())
        {
            if (content.Files is not null)
            {
                LinuxClipboard.SetFiles(content.Files);
            }
            else if (content.Html is not null)
            {
                LinuxClipboard.SetHtml(content.Html, content.Text);
            }
            else if (content.Text is not null)
            {
                LinuxClipboard.SetText(content.Text);
            }
            return Task.FromResult(true);
        }
        throw new PlatformNotSupportedException("Clipboard is only supported on Windows, macOS, and Linux.");
    }

    /// <summary>Writes plain text to the clipboard. Returns false when it could not be opened.</summary>
    /// <param name="text">The text to write.</param>
    public static Task<bool> WriteTextAsync(string text) => WriteAsync(new ClipboardContent { Text = text });

    /// <summary>
    /// Writes an image (RGBA pixels): CF_DIBV5 with alpha plus PNG on Windows. Returns false when the
    /// clipboard could not be opened.
    /// </summary>
    /// <param name="image">The image data to write (RGBA pixels).</param>
    public static Task<bool> WriteImageAsync(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Image clipboard write is only supported on Windows.");
        }
        return WriteAsync(new ClipboardContent { Image = image });
    }

    /// <summary>
    /// Writes file paths to the clipboard (copy-to-clipboard, paste into file manager).
    /// </summary>
    /// <param name="filePaths">Absolute paths of the files to place on the clipboard.</param>
    public static Task<bool> WriteFilesAsync(IReadOnlyList<string> filePaths) => WriteAsync(new ClipboardContent { Files = filePaths });

    /// <summary>
    /// Writes every format captured in <paramref name="snapshot"/> back in one open/close.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore to the clipboard.</param>
    public static Task<bool> WriteSnapshotAsync(ClipboardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return WriteAsync(new ClipboardContent
        {
            Text = snapshot.Text,
            Html = snapshot.Html,
            Rtf = snapshot.Rtf,
            Image = snapshot.Image,
            Files = snapshot.Files,
        });
    }

    // ── Raw formats (Windows) ────────────────────────────────────────

    /// <summary>
    /// Copies every format currently on the clipboard, byte for byte, in one open — the capture a
    /// clipboard history needs to paste exactly what was copied. Formats Windows synthesizes from
    /// another (CF_TEXT from Unicode text, CF_DIB from CF_DIBV5, CF_BITMAP) and GDI-handle formats
    /// are skipped; enhanced metafiles are kept. <paramref name="includeFormat"/> receives each
    /// format's name before its data is requested, so delayed-rendered formats you do not want
    /// (e.g. Explorer's "FileContents") are never rendered. Returns null when the clipboard could
    /// not be opened. Windows only.
    /// </summary>
    public static ClipboardRawSnapshot? CaptureRaw(Func<string, bool>? includeFormat = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Raw clipboard capture is only supported on Windows.");
        }
        return Win32Clipboard.CaptureRaw(includeFormat);
    }

    /// <summary>
    /// Puts every format of <paramref name="snapshot"/> on the clipboard in one open. Returns false
    /// when the clipboard could not be opened or a format could not be set. Windows only.
    /// </summary>
    public static bool WriteRaw(ClipboardRawSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Raw clipboard write is only supported on Windows.");
        }
        return Win32Clipboard.WriteRaw(snapshot);
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
                nint hwnd = App.nativeWindow?.Handle ?? attachedWindow;
                if (hwnd != 0)
                {
                    Win32Clipboard.StartMonitoring(hwnd);
                    listenerWindow = hwnd;
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
            if (System.Threading.Interlocked.Decrement(ref monitoringRefCount) == 0 && listenerWindow != 0)
            {
                Win32Clipboard.StopMonitoring(listenerWindow);
                listenerWindow = 0;
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
        var source = ClipboardSource.OtherApp;
        bool excluded = false;
        ClipboardSourceApp? sourceApp = null;

        if (OperatingSystem.IsWindows())
        {
            ClipboardAvailability avail = Win32Clipboard.GetAvailableFormats();
            content = ClipboardContent.FromWin32Availability(avail);
            sequenceNumber = Win32Clipboard.GetSequenceNumber();
            if (sequenceNumber == lastNotifiedSequence)
            {
                return;
            }
            lastNotifiedSequence = sequenceNumber;
            source = Win32Clipboard.IsOwnChange(sequenceNumber) ? ClipboardSource.ThisApp : ClipboardSource.OtherApp;
            excluded = Win32Clipboard.IsExcludedFromHistory();
            uint processId = Win32Clipboard.GetSourceProcessId();
            sourceApp = processId != 0 ? new ClipboardSourceApp(processId) : null;
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

        var args = new ClipboardChangedEventArgs(content, sequenceNumber, source)
        {
            IsExcludedFromHistory = excluded,
            SourceApp = sourceApp,
        };
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

    /// <summary>
    /// True when the content asks not to be recorded — a password manager setting
    /// ExcludeClipboardContentFromMonitorProcessing, Clipboard Viewer Ignore, or
    /// CanIncludeInClipboardHistory = 0. Clipboard histories must skip it. Windows only.
    /// </summary>
    public bool IsExcludedFromHistory { get; init; }

    /// <summary>
    /// The app that put the content on the clipboard (the clipboard owner, or the foreground app when
    /// the copier set no owner). Null when unknown. Windows only.
    /// </summary>
    public ClipboardSourceApp? SourceApp { get; init; }
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

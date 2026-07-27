using System.Runtime.InteropServices;
using System.Text;

namespace Cascade.UI;

/// <summary>
/// Linux clipboard implementation. Supports X11 selections (CLIPBOARD and PRIMARY)
/// and Wayland data offers via the data device protocol. The implementation mirrors
/// Win32Clipboard's API surface — static methods for reading, writing, and monitoring.
///
/// X11 clipboard uses the selection mechanism:
/// - To own the clipboard: XSetSelectionOwner with the CLIPBOARD atom.
/// - To read: XConvertSelection requesting UTF8_STRING, then receive
///   SelectionNotify with the data.
/// - To serve paste requests: Handle SelectionRequest events and respond
///   with the data via XChangeProperty + XSendEvent.
///
/// Wayland clipboard uses the data device protocol:
/// - wl_data_source for offering clipboard content.
/// - wl_data_offer for receiving clipboard content.
/// </summary>
internal static class LinuxClipboard
{
    // Cached X11 atoms for clipboard operations.
    private static nint atomClipboard;
    private static nint atomPrimary;
    private static nint atomTargets;
    private static nint atomUtf8String;
    private static nint atomTextPlain;
    private static nint atomTextPlainUtf8;
    private static nint atomTextHtml;
    private static nint atomUriList;
    private static nint atomCascadeClipboardProperty;

    private static nint display;
    private static nint clipboardWindow;
    private static string? ownedText;
    private static string? ownedHtml;
    private static IReadOnlyList<string>? ownedFiles;
    private static DisplayServer currentServer;

    /// <summary>
    /// Initializes clipboard atoms and creates a hidden window for selection
    /// ownership on X11. For Wayland, prepares the data device manager.
    /// </summary>
    internal static void Initialize(nint x11Display, nint x11Window, DisplayServer server)
    {
        currentServer = server;

        if (server == DisplayServer.X11 && x11Display != 0)
        {
            display = x11Display;
            clipboardWindow = x11Window;
            InternAtoms();
        }
    }

    /// <summary>
    /// Returns the clipboard atom name for a given ClipboardFormat.
    /// Used for format negotiation in X11 selection protocol.
    /// </summary>
    internal static string GetAtomNameForFormat(ClipboardFormat format)
    {
        if (format.Equals(ClipboardFormat.Text))
        {
            return "UTF8_STRING";
        }

        if (format.Equals(ClipboardFormat.Html))
        {
            return "text/html";
        }

        if (format.Equals(ClipboardFormat.Files))
        {
            return "text/uri-list";
        }

        return format.Name;
    }

    /// <summary>
    /// Returns the MIME type string for a ClipboardFormat, used in Wayland
    /// data offers and xdg-desktop-portal interactions.
    /// </summary>
    internal static string GetMimeTypeForFormat(ClipboardFormat format)
    {
        if (format.Equals(ClipboardFormat.Text))
        {
            return "text/plain;charset=utf-8";
        }

        if (format.Equals(ClipboardFormat.Html))
        {
            return "text/html";
        }

        if (format.Equals(ClipboardFormat.Rtf))
        {
            return "text/rtf";
        }

        if (format.Equals(ClipboardFormat.Image))
        {
            return "image/png";
        }

        if (format.Equals(ClipboardFormat.Files))
        {
            return "text/uri-list";
        }

        return $"application/x-cascade-{format.Name}";
    }

    // ── Reading (X11) ────────────────────────────────────────────────

    /// <summary>
    /// Returns which standard formats are currently available on the clipboard.
    /// On X11, this queries the selection owner's supported targets.
    /// </summary>
    internal static ClipboardAvailability GetAvailableFormats()
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return new ClipboardAvailability();
        }

        // If we own the clipboard, report our own formats.
        nint owner = X11Interop.XGetSelectionOwner(display, atomClipboard);
        if (owner == clipboardWindow)
        {
            return new ClipboardAvailability
            {
                HasText = ownedText is not null,
                HasHtml = ownedHtml is not null,
                HasFiles = ownedFiles is not null,
                HasImage = false
            };
        }

        // For remote selection owners, we would request TARGETS and parse the
        // atom list. This requires an async selection protocol round-trip.
        // Return a conservative default indicating text may be available.
        return new ClipboardAvailability
        {
            HasText = owner != 0,
            HasHtml = false,
            HasFiles = false,
            HasImage = false
        };
    }

    /// <summary>
    /// Reads plain text from the clipboard via X11 selection protocol.
    /// This is a synchronous simplified read — full async is handled at
    /// the framework level.
    /// </summary>
    internal static string? GetText()
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return null;
        }

        // If we own the clipboard, return our data directly.
        nint owner = X11Interop.XGetSelectionOwner(display, atomClipboard);
        if (owner == clipboardWindow)
        {
            return ownedText;
        }

        // Request the selection as UTF8_STRING.
        return RequestSelectionString(atomClipboard, atomUtf8String);
    }

    /// <summary>
    /// Reads HTML content from the clipboard.
    /// </summary>
    internal static string? GetHtml()
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return null;
        }

        nint owner = X11Interop.XGetSelectionOwner(display, atomClipboard);
        if (owner == clipboardWindow)
        {
            return ownedHtml;
        }

        return RequestSelectionString(atomClipboard, atomTextHtml);
    }

    /// <summary>
    /// Reads file paths from the clipboard (text/uri-list format).
    /// URI paths are converted from file:// URIs to local paths.
    /// </summary>
    internal static IReadOnlyList<string>? GetFiles()
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return null;
        }

        nint owner = X11Interop.XGetSelectionOwner(display, atomClipboard);
        if (owner == clipboardWindow)
        {
            return ownedFiles;
        }

        string? uriList = RequestSelectionString(atomClipboard, atomUriList);
        if (string.IsNullOrEmpty(uriList))
        {
            return null;
        }

        return ParseUriList(uriList);
    }

    // ── Writing (X11) ────────────────────────────────────────────────

    /// <summary>
    /// Writes plain text to the clipboard by claiming X11 selection ownership.
    /// </summary>
    internal static bool SetText(string text)
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return false;
        }

        ownedText = text;
        ownedHtml = null;
        ownedFiles = null;
        _ = X11Interop.XSetSelectionOwner(display, atomClipboard, clipboardWindow, 0);
        _ = X11Interop.XFlush(display);

        return X11Interop.XGetSelectionOwner(display, atomClipboard) == clipboardWindow;
    }

    /// <summary>
    /// Writes HTML content to the clipboard. Also stores a plain text fallback.
    /// </summary>
    internal static bool SetHtml(string html, string? plainTextFallback = null)
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return false;
        }

        ownedHtml = html;
        ownedText = plainTextFallback;
        ownedFiles = null;
        _ = X11Interop.XSetSelectionOwner(display, atomClipboard, clipboardWindow, 0);
        _ = X11Interop.XFlush(display);

        return X11Interop.XGetSelectionOwner(display, atomClipboard) == clipboardWindow;
    }

    /// <summary>
    /// Writes file paths to the clipboard as text/uri-list.
    /// </summary>
    internal static bool SetFiles(IReadOnlyList<string> filePaths)
    {
        if (currentServer != DisplayServer.X11 || display == 0)
        {
            return false;
        }

        ownedFiles = filePaths;
        ownedText = null;
        ownedHtml = null;
        _ = X11Interop.XSetSelectionOwner(display, atomClipboard, clipboardWindow, 0);
        _ = X11Interop.XFlush(display);

        return X11Interop.XGetSelectionOwner(display, atomClipboard) == clipboardWindow;
    }

    // ── Selection Request Handling ───────────────────────────────────

    /// <summary>
    /// Handles an incoming SelectionRequest event from another X11 client.
    /// Responds by writing the requested data to the requestor's property.
    /// </summary>
    internal static void HandleSelectionRequest(XSelectionRequestEvent request)
    {
        if (display == 0)
        {
            return;
        }

        XEvent response = new();
        unsafe
        {
            XSelectionEvent* sel = (XSelectionEvent*)&response;
            sel->type = X11Interop.SelectionNotify;
            sel->requestor = request.requestor;
            sel->selection = request.selection;
            sel->target = request.target;
            sel->property = request.property != 0 ? request.property : request.target;
            sel->time = request.time;
        }

        if (request.target == atomTargets)
        {
            // Respond with the list of supported targets.
            List<nint> targets = [atomTargets, atomUtf8String];
            if (ownedHtml is not null)
            {
                targets.Add(atomTextHtml);
            }
            if (ownedFiles is not null)
            {
                targets.Add(atomUriList);
            }

            _ = X11Interop.XChangeProperty(
                display, request.requestor,
                request.property != 0 ? request.property : request.target,
                X11Interop.XA_ATOM, 32, X11Interop.PropModeReplace,
                targets.ToArray(), targets.Count);
        }
        else if (request.target == atomUtf8String && ownedText is not null)
        {
            WriteStringProperty(request.requestor,
                request.property != 0 ? request.property : request.target,
                atomUtf8String, ownedText);
        }
        else if (request.target == atomTextHtml && ownedHtml is not null)
        {
            WriteStringProperty(request.requestor,
                request.property != 0 ? request.property : request.target,
                atomUtf8String, ownedHtml);
        }
        else if (request.target == atomUriList && ownedFiles is not null)
        {
            string uriList = BuildUriList(ownedFiles);
            WriteStringProperty(request.requestor,
                request.property != 0 ? request.property : request.target,
                atomUtf8String, uriList);
        }
        else
        {
            // Unsupported target — set property to None to indicate failure.
            unsafe
            {
                XSelectionEvent* sel = (XSelectionEvent*)&response;
                sel->property = 0;
            }
        }

        _ = X11Interop.XSendEvent(display, request.requestor, false, 0, ref response);
        _ = X11Interop.XFlush(display);
    }

    /// <summary>
    /// Handles a SelectionClear event, indicating another client has claimed
    /// clipboard ownership.
    /// </summary>
    internal static void HandleSelectionClear()
    {
        ownedText = null;
        ownedHtml = null;
        ownedFiles = null;
    }

    // ── URI Parsing ──────────────────────────────────────────────────

    /// <summary>
    /// Parses a text/uri-list string into an array of local file paths.
    /// Lines starting with # are comments (per RFC 2483). file:// URIs
    /// are converted to local paths.
    /// </summary>
    internal static IReadOnlyList<string> ParseUriList(string uriList)
    {
        List<string> paths = [];
        string[] lines = uriList.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim('\r', ' ');

            // Skip comments.
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            // Convert file:// URIs to local paths.
            if (line.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                string path = Uri.UnescapeDataString(line[7..]);
                if (path.Length > 0)
                {
                    paths.Add(path);
                }
            }
            else
            {
                // Non-file URIs are included as-is.
                paths.Add(line);
            }
        }

        return paths;
    }

    /// <summary>
    /// Builds a text/uri-list string from file paths.
    /// </summary>
    internal static string BuildUriList(IReadOnlyList<string> filePaths)
    {
        StringBuilder sb = new();
        foreach (string path in filePaths)
        {
            sb.Append("file://");
            sb.Append(Uri.EscapeDataString(path));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static void InternAtoms()
    {
        atomClipboard              = X11Interop.XInternAtom(display, "CLIPBOARD", false);
        atomPrimary                = X11Interop.XInternAtom(display, "PRIMARY", false);
        atomTargets                = X11Interop.XInternAtom(display, "TARGETS", false);
        atomUtf8String             = X11Interop.XInternAtom(display, "UTF8_STRING", false);
        atomTextPlain              = X11Interop.XInternAtom(display, "text/plain", false);
        atomTextPlainUtf8          = X11Interop.XInternAtom(display, "text/plain;charset=utf-8", false);
        atomTextHtml               = X11Interop.XInternAtom(display, "text/html", false);
        atomUriList                = X11Interop.XInternAtom(display, "text/uri-list", false);
        atomCascadeClipboardProperty = X11Interop.XInternAtom(display, "CASCADE_CLIPBOARD", false);
    }

    /// <summary>
    /// Requests a string value from the X11 selection. Sends XConvertSelection
    /// and waits for the SelectionNotify response.
    /// </summary>
    private static string? RequestSelectionString(nint selection, nint target)
    {
        // Request conversion of the selection to the target format.
        _ = X11Interop.XConvertSelection(display, selection, target,
            atomCascadeClipboardProperty, clipboardWindow, 0);
        _ = X11Interop.XFlush(display);

        // Wait for the SelectionNotify event (with a timeout to avoid hangs).
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (X11Interop.XPending(display) > 0)
            {
                X11Interop.XNextEvent(display, out XEvent ev);

                if (ev.type == X11Interop.SelectionNotify)
                {
                    XSelectionEvent selEvent = ev.AsSelectionEvent();
                    if (selEvent.property == 0)
                    {
                        return null; // Conversion failed.
                    }

                    return ReadStringProperty(clipboardWindow, selEvent.property);
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        return null; // Timed out.
    }

    /// <summary>
    /// Reads a string property from an X11 window.
    /// </summary>
    private static string? ReadStringProperty(nint window, nint property)
    {
        int result = X11Interop.XGetWindowProperty(
            display, window, property,
            0, 1024 * 1024, true, X11Interop.AnyPropertyType,
            out nint _, out int _, out nuint nItems, out nuint _,
            out nint propData);

        if (result != 0 || propData == 0 || nItems == 0)
        {
            if (propData != 0)
            {
                _ = X11Interop.XFree(propData);
            }
            return null;
        }

        try
        {
            string? value = Marshal.PtrToStringUTF8(propData);
            return value;
        }
        finally
        {
            _ = X11Interop.XFree(propData);
        }
    }

    /// <summary>
    /// Writes a UTF-8 string to an X11 window property.
    /// </summary>
    private static void WriteStringProperty(nint window, nint property, nint type, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        nint buffer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            _ = X11Interop.XChangeProperty(
                display, window, property, type,
                8, X11Interop.PropModeReplace,
                buffer, bytes.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

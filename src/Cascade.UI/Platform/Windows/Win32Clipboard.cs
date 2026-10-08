using System.Runtime.InteropServices;
using System.Text;
using Cascade.UI.Imaging;

namespace Cascade.UI;

/// <summary>
/// Win32 clipboard implementation: typed reads and writes (Unicode text, CF_HTML, RTF, images,
/// file lists), raw multi-format capture and restore, change attribution and exclusion markers.
/// </summary>
/// <remarks>
/// Every access opens the clipboard with this app's window as owner and retries for a short time:
/// another process may hold it open, and a single failed OpenClipboard used to drop the operation.
/// </remarks>
internal static unsafe class Win32Clipboard
{
    // Registered formats. Registration used to live in an Initialize() that nothing called, so HTML
    // and RTF were never detected or written.
    internal static readonly uint CfHtml = Win32.RegisterClipboardFormatW("HTML Format");
    internal static readonly uint CfRtf = Win32.RegisterClipboardFormatW("Rich Text Format");
    internal static readonly uint CfPng = Win32.RegisterClipboardFormatW("PNG");

    // Content a password manager or similar app marks as not for clipboard history.
    private static readonly uint CfExcludeFromMonitoring = Win32.RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CfViewerIgnore = Win32.RegisterClipboardFormatW("Clipboard Viewer Ignore");
    private static readonly uint CfCanIncludeInHistory = Win32.RegisterClipboardFormatW("CanIncludeInClipboardHistory");

    // The sequence number right after our last write: the WM_CLIPBOARDUPDATE it causes is ours.
    private static uint lastOwnSequence;

    // ── Session ──────────────────────────────────────────────────────

    private static bool Open()
    {
        nint owner = App.nativeWindow?.Handle ?? 0;
        for (int attempt = 0; ; attempt++)
        {
            if (Win32.OpenClipboard(owner))
            {
                return true;
            }
            if (attempt == 8)
            {
                return false;
            }
            Thread.Sleep(Math.Min(5 << attempt, 100)); // 5, 10, 20, … ms: about 0.4 s in total
        }
    }

    private static bool OpenForWrite()
    {
        if (!Open())
        {
            return false;
        }
        Win32.EmptyClipboard();
        return true;
    }

    private static void CloseAfterWrite()
    {
        Win32.CloseClipboard();
        lastOwnSequence = Win32.GetClipboardSequenceNumber();
    }

    /// <summary>Whether the clipboard content with this sequence number was written by this app.</summary>
    internal static bool IsOwnChange(uint sequence)
    {
        nint window = App.nativeWindow?.Handle ?? 0;
        return sequence == lastOwnSequence || (window != 0 && Win32.GetClipboardOwner() == window);
    }

    // ── Availability, exclusion, source ──────────────────────────────

    internal static ClipboardAvailability GetAvailableFormats()
    {
        return new ClipboardAvailability
        {
            HasText = Win32.IsClipboardFormatAvailable(Win32.CF_UNICODETEXT),
            HasHtml = CfHtml != 0 && Win32.IsClipboardFormatAvailable(CfHtml),
            HasRtf = CfRtf != 0 && Win32.IsClipboardFormatAvailable(CfRtf),
            HasFiles = Win32.IsClipboardFormatAvailable(Win32.CF_HDROP),
            HasImage = Win32.IsClipboardFormatAvailable(Win32.CF_DIBV5)
                || Win32.IsClipboardFormatAvailable(Win32.CF_DIB)
                || (CfPng != 0 && Win32.IsClipboardFormatAvailable(CfPng)),
        };
    }

    /// <summary>
    /// Whether the current content asks not to be recorded: ExcludeClipboardContentFromMonitorProcessing
    /// or Clipboard Viewer Ignore present, or CanIncludeInClipboardHistory set to 0.
    /// </summary>
    internal static bool IsExcludedFromHistory()
    {
        if (Win32.IsClipboardFormatAvailable(CfExcludeFromMonitoring) || Win32.IsClipboardFormatAvailable(CfViewerIgnore))
        {
            return true;
        }
        if (!Win32.IsClipboardFormatAvailable(CfCanIncludeInHistory) || !Open())
        {
            return false;
        }
        try
        {
            byte[]? value = ReadBytes(CfCanIncludeInHistory);
            return value is { Length: >= 4 } && BitConverter.ToUInt32(value, 0) == 0;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// The process that put the content on the clipboard: the clipboard owner window's process, or —
    /// for apps that open the clipboard without an owner — the foreground window's.
    /// </summary>
    internal static uint GetSourceProcessId()
    {
        nint window = Win32.GetClipboardOwner();
        if (window == 0)
        {
            window = Win32.GetForegroundWindow();
        }
        if (window == 0)
        {
            return 0;
        }
        Win32.GetWindowThreadProcessId(window, out uint processId);
        return processId;
    }

    internal static string? GetProcessPath(uint processId)
    {
        nint process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return null;
        }
        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            return Win32.QueryFullProcessImageNameW(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            Win32.CloseHandle(process);
        }
    }

    internal static uint GetSequenceNumber() => Win32.GetClipboardSequenceNumber();

    // ── Typed reads ──────────────────────────────────────────────────

    internal static string? GetText()
    {
        byte[]? bytes = ReadFormat(Win32.CF_UNICODETEXT);
        return bytes is null ? null : DecodeUnicode(bytes);
    }

    /// <summary>The HTML fragment of the CF_HTML content.</summary>
    internal static string? GetHtml()
    {
        byte[]? bytes = ReadFormat(CfHtml);
        return bytes is null ? null : ExtractHtmlFragment(bytes);
    }

    internal static string? GetRtf()
    {
        byte[]? bytes = ReadFormat(CfRtf);
        return bytes is null ? null : Encoding.UTF8.GetString(TrimNull(bytes));
    }

    internal static IReadOnlyList<string>? GetFiles()
    {
        if (!Open())
        {
            return null;
        }
        try
        {
            nint hData = Win32.GetClipboardData(Win32.CF_HDROP);
            if (hData == 0)
            {
                return null;
            }

            uint fileCount = Win32.DragQueryFileW(hData, 0xFFFFFFFF, null, 0);
            var files = new List<string>((int)fileCount);
            for (uint i = 0; i < fileCount; i++)
            {
                uint charCount = Win32.DragQueryFileW(hData, i, null, 0);
                if (charCount == 0)
                {
                    continue;
                }
                char[] buffer = new char[charCount + 1];
                Win32.DragQueryFileW(hData, i, buffer, (uint)buffer.Length);
                files.Add(new string(buffer, 0, (int)charCount));
            }
            return files;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// The image as RGBA. PNG is preferred when offered (it carries exact alpha); otherwise
    /// CF_DIBV5 or CF_DIB, honouring BI_BITFIELDS masks.
    /// </summary>
    internal static ImageData? GetImage()
    {
        if (ReadFormat(CfPng) is { } png)
        {
            try
            {
                var (rgba, width, height) = ImageCodec.DecodeToRgba(png);
                return new ImageData { Pixels = rgba, Width = width, Height = height, Stride = width * 4 };
            }
            catch (InvalidDataException)
            {
                // Fall back to the DIB.
            }
        }

        byte[]? dib = ReadFormat(Win32.IsClipboardFormatAvailable(Win32.CF_DIBV5) ? Win32.CF_DIBV5 : Win32.CF_DIB);
        return dib is null ? null : DecodeDib(dib);
    }

    private static byte[]? ReadFormat(uint format)
    {
        if (format == 0 || !Win32.IsClipboardFormatAvailable(format) || !Open())
        {
            return null;
        }
        try
        {
            return ReadBytes(format);
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    // Copies an HGLOBAL-backed format. The clipboard must be open; the handle stays the clipboard's.
    private static byte[]? ReadBytes(uint format)
    {
        nint hData = Win32.GetClipboardData(format);
        if (hData == 0)
        {
            return null;
        }
        nuint size = Win32.GlobalSize(hData);
        nint pData = Win32.GlobalLock(hData);
        if (pData == 0)
        {
            return null;
        }
        try
        {
            return new ReadOnlySpan<byte>((void*)pData, (int)size).ToArray();
        }
        finally
        {
            Win32.GlobalUnlock(hData);
        }
    }

    // ── Typed writes ─────────────────────────────────────────────────

    /// <summary>
    /// Writes every given format in one open/close so apps pick the richest they support. Returns
    /// false when the clipboard could not be opened.
    /// </summary>
    internal static bool Write(string? text, string? html, string? rtf, ImageData? image, IReadOnlyList<string>? files)
    {
        if (!OpenForWrite())
        {
            return false;
        }
        try
        {
            if (files is not null)
            {
                PutFiles(files);
            }
            if (image is not null)
            {
                PutImage(image);
            }
            if (rtf is not null)
            {
                PutBytes(CfRtf, NullTerminated(Encoding.UTF8.GetBytes(rtf)));
            }
            if (html is not null)
            {
                PutBytes(CfHtml, NullTerminated(BuildCfHtml(html)));
            }
            if (text is not null)
            {
                PutText(text);
            }
            return true;
        }
        finally
        {
            CloseAfterWrite();
        }
    }

    internal static bool SetText(string text) => Write(text, null, null, null, null);

    internal static bool SetHtml(string html, string? plainTextFallback = null) => Write(plainTextFallback, html, null, null, null);

    internal static bool SetImage(ImageData image) => Write(null, null, null, image, null);

    internal static bool SetFiles(IReadOnlyList<string> filePaths) => Write(null, null, null, null, filePaths);

    private static void PutText(string text)
    {
        byte[] bytes = new byte[(text.Length + 1) * sizeof(char)];
        MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(bytes);
        PutBytes(Win32.CF_UNICODETEXT, bytes);
    }

    // Allocates a movable global block with the data and hands it to the clipboard (which then owns it).
    private static bool PutBytes(uint format, ReadOnlySpan<byte> data)
    {
        if (format == 0)
        {
            return false;
        }
        nint hGlobal = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (nuint)Math.Max(1, data.Length));
        if (hGlobal == 0)
        {
            return false;
        }
        nint p = Win32.GlobalLock(hGlobal);
        if (p == 0)
        {
            Win32.GlobalFree(hGlobal);
            return false;
        }
        data.CopyTo(new Span<byte>((void*)p, data.Length));
        Win32.GlobalUnlock(hGlobal);
        if (Win32.SetClipboardData(format, hGlobal) == 0)
        {
            Win32.GlobalFree(hGlobal);
            return false;
        }
        return true;
    }

    private static void PutFiles(IReadOnlyList<string> filePaths)
    {
        // DROPFILES header, then each path null-terminated, then a final null.
        int headerSize = Marshal.SizeOf<Win32.DROPFILES>();
        int chars = 1;
        foreach (string path in filePaths)
        {
            chars += path.Length + 1;
        }
        byte[] block = new byte[headerSize + chars * sizeof(char)];
        fixed (byte* p = block)
        {
            *(Win32.DROPFILES*)p = new Win32.DROPFILES { pFiles = (uint)headerSize, fWide = 1 };
            char* cursor = (char*)(p + headerSize);
            foreach (string path in filePaths)
            {
                path.AsSpan().CopyTo(new Span<char>(cursor, path.Length));
                cursor += path.Length + 1;
            }
        }
        PutBytes(Win32.CF_HDROP, block);
    }

    // CF_DIBV5 with an alpha mask (bottom-up BGRA, sRGB) plus "PNG": apps that ignore DIB alpha
    // (most) read the PNG; Windows synthesizes CF_DIB/CF_BITMAP from the V5.
    private static void PutImage(ImageData image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Pixels.Length == 0)
        {
            return;
        }

        const int headerSize = 124;
        int rowBytes = image.Width * 4;
        int srcStride = image.Stride > 0 ? image.Stride : rowBytes;
        byte[] dib = new byte[headerSize + rowBytes * image.Height];
        fixed (byte* p = dib)
        {
            var h = (Win32.BITMAPV5HEADER*)p;
            h->bV5Size = headerSize;
            h->bV5Width = image.Width;
            h->bV5Height = image.Height; // bottom-up
            h->bV5Planes = 1;
            h->bV5BitCount = 32;
            h->bV5Compression = Win32.BI_BITFIELDS;
            h->bV5SizeImage = (uint)(rowBytes * image.Height);
            h->bV5RedMask = 0x00FF0000;
            h->bV5GreenMask = 0x0000FF00;
            h->bV5BlueMask = 0x000000FF;
            h->bV5AlphaMask = 0xFF000000;
            h->bV5CSType = 0x73524742; // 'sRGB'
            h->bV5Intent = 4;          // LCS_GM_IMAGES
        }

        var src = image.Pixels.AsSpan();
        for (int y = 0; y < image.Height; y++)
        {
            var srcRow = src.Slice(y * srcStride, rowBytes);
            var dstRow = dib.AsSpan(headerSize + (image.Height - 1 - y) * rowBytes, rowBytes);
            for (int x = 0; x < rowBytes; x += 4)
            {
                dstRow[x] = srcRow[x + 2];
                dstRow[x + 1] = srcRow[x + 1];
                dstRow[x + 2] = srcRow[x];
                dstRow[x + 3] = srcRow[x + 3];
            }
        }

        PutBytes(Win32.CF_DIBV5, dib);
        PutBytes(CfPng, ImageCodec.EncodePng(image.Pixels, image.Width, image.Height, srcStride));
    }

    // ── Raw capture and restore ──────────────────────────────────────

    /// <summary>
    /// Copies every format on the clipboard, in the order offered, in one open. Skipped: formats that
    /// Windows synthesizes from one already captured (CF_TEXT/CF_OEMTEXT from Unicode text, the
    /// second of CF_DIB/CF_DIBV5, CF_BITMAP), and handle formats that are not memory blocks (GDI
    /// objects, palettes, metafile pictures, owner-display, private). CF_ENHMETAFILE is kept as its
    /// bytes. <paramref name="include"/> sees each format's name first, so expensive delayed-rendered
    /// formats (e.g. Explorer's "FileContents") are never requested.
    /// </summary>
    internal static ClipboardRawSnapshot? CaptureRaw(Func<string, bool>? include)
    {
        if (!Open())
        {
            return null;
        }
        try
        {
            var formats = new List<ClipboardRawFormat>();
            bool haveUnicodeText = false;
            bool haveAnsiText = false;
            bool haveDib = false;
            uint format = 0;
            while ((format = Win32.EnumClipboardFormats(format)) != 0)
            {
                if (IsHandleFormat(format))
                {
                    continue;
                }
                // Windows lists the formats the owner put first and the ones it synthesizes after.
                // Unicode text is always kept: it is what readers want, and when the owner put only
                // ANSI text it is Windows' own conversion of it (CF_LOCALE). An ANSI format is kept
                // only when it came first (the owner's own bytes, restored exactly); one listed after
                // other text is synthesized and redundant.
                if (format == Win32.CF_UNICODETEXT)
                {
                    if (haveUnicodeText)
                    {
                        continue;
                    }
                    haveUnicodeText = true;
                }
                else if (format is Win32.CF_TEXT or Win32.CF_OEMTEXT)
                {
                    if (haveUnicodeText || haveAnsiText)
                    {
                        continue;
                    }
                    haveAnsiText = true;
                }
                if (format is Win32.CF_DIB or Win32.CF_DIBV5)
                {
                    if (haveDib)
                    {
                        continue;
                    }
                    haveDib = true;
                }

                string name = FormatName(format);
                if (include is not null && !include(name))
                {
                    continue;
                }

                byte[]? data = format == Win32.CF_ENHMETAFILE ? ReadEnhMetaFile() : ReadBytes(format);
                if (data is not null)
                {
                    formats.Add(new ClipboardRawFormat(format, name, data));
                }
            }
            return new ClipboardRawSnapshot(formats);
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>Puts every format of a raw snapshot back on the clipboard in one open.</summary>
    internal static bool WriteRaw(ClipboardRawSnapshot snapshot)
    {
        if (!OpenForWrite())
        {
            return false;
        }
        try
        {
            bool all = true;
            foreach (var entry in snapshot.Formats)
            {
                // Registered ids are per session; re-resolve them by name.
                uint format = entry.Id >= 0xC000 ? Win32.RegisterClipboardFormatW(entry.Name) : entry.Id;
                all &= format == Win32.CF_ENHMETAFILE ? PutEnhMetaFile(entry.Data.Span) : PutBytes(format, entry.Data.Span);
            }
            return all;
        }
        finally
        {
            CloseAfterWrite();
        }
    }

    private static bool IsHandleFormat(uint format)
    {
        return format is Win32.CF_BITMAP or Win32.CF_PALETTE or Win32.CF_METAFILEPICT or Win32.CF_OWNERDISPLAY
            || (format >= Win32.CF_DSPTEXT && format <= Win32.CF_DSPENHMETAFILE)
            || (format >= Win32.CF_PRIVATEFIRST && format <= Win32.CF_GDIOBJLAST);
    }

    private static string FormatName(uint format)
    {
        char* buffer = stackalloc char[256];
        int length = Win32.GetClipboardFormatNameW(format, buffer, 256);
        if (length > 0)
        {
            return new string(buffer, 0, length);
        }
        return format switch
        {
            Win32.CF_TEXT => "CF_TEXT",
            Win32.CF_OEMTEXT => "CF_OEMTEXT",
            Win32.CF_UNICODETEXT => "CF_UNICODETEXT",
            Win32.CF_DIB => "CF_DIB",
            Win32.CF_DIBV5 => "CF_DIBV5",
            Win32.CF_HDROP => "CF_HDROP",
            Win32.CF_LOCALE => "CF_LOCALE",
            Win32.CF_ENHMETAFILE => "CF_ENHMETAFILE",
            _ => $"#{format}",
        };
    }

    private static byte[]? ReadEnhMetaFile()
    {
        nint hemf = Win32.GetClipboardData(Win32.CF_ENHMETAFILE);
        if (hemf == 0)
        {
            return null;
        }
        uint size = Win32.GetEnhMetaFileBits(hemf, 0, null);
        if (size == 0)
        {
            return null;
        }
        byte[] data = new byte[size];
        fixed (byte* p = data)
        {
            if (Win32.GetEnhMetaFileBits(hemf, size, p) != size)
            {
                return null;
            }
        }
        return data;
    }

    private static bool PutEnhMetaFile(ReadOnlySpan<byte> data)
    {
        nint hemf;
        fixed (byte* p = data)
        {
            hemf = Win32.SetEnhMetaFileBits((uint)data.Length, p);
        }
        if (hemf == 0)
        {
            return false;
        }
        if (Win32.SetClipboardData(Win32.CF_ENHMETAFILE, hemf) == 0)
        {
            Win32.DeleteEnhMetaFile(hemf);
            return false;
        }
        return true;
    }

    // ── Monitoring ───────────────────────────────────────────────────

    internal static void StartMonitoring(nint hWnd) => Win32.AddClipboardFormatListener(hWnd);

    internal static void StopMonitoring(nint hWnd) => Win32.RemoveClipboardFormatListener(hWnd);

    // ── Encoding helpers ─────────────────────────────────────────────

    private static string DecodeUnicode(byte[] bytes)
    {
        var chars = MemoryMarshal.Cast<byte, char>(bytes.AsSpan(0, bytes.Length & ~1));
        int end = chars.IndexOf('\0');
        return new string(end >= 0 ? chars[..end] : chars);
    }

    private static ReadOnlySpan<byte> TrimNull(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        return end >= 0 ? bytes.AsSpan(0, end) : bytes;
    }

    private static byte[] NullTerminated(byte[] bytes)
    {
        byte[] result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        return result;
    }

    /// <summary>
    /// The fragment of CF_HTML data. StartFragment/EndFragment are byte offsets into the UTF-8 data;
    /// slicing the decoded string with them (as this used to) cut non-ASCII HTML in the wrong place.
    /// </summary>
    internal static string ExtractHtmlFragment(byte[] data)
    {
        var bytes = TrimNull(data);
        string header = Encoding.ASCII.GetString(bytes[..Math.Min(bytes.Length, 512)]);
        int start = ReadOffset(header, "StartFragment:");
        int end = ReadOffset(header, "EndFragment:");
        if (start >= 0 && end > start && end <= bytes.Length)
        {
            return Encoding.UTF8.GetString(bytes[start..end]);
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static int ReadOffset(string header, string key)
    {
        int at = header.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
        {
            return -1;
        }
        at += key.Length;
        int stop = at;
        while (stop < header.Length && char.IsAsciiDigit(header[stop]))
        {
            stop++;
        }
        return int.TryParse(header.AsSpan(at, stop - at), out int value) ? value : -1;
    }

    /// <summary>Builds CF_HTML (UTF-8 bytes, byte offsets in the header) around a fragment.</summary>
    internal static byte[] BuildCfHtml(string htmlFragment)
    {
        const string headerFormat = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string prefix = "<html><body>\r\n<!--StartFragment-->";
        const string suffix = "<!--EndFragment-->\r\n</body></html>";

        int headerBytes = string.Format(System.Globalization.CultureInfo.InvariantCulture, headerFormat, 0, 0, 0, 0).Length;
        int startFragment = headerBytes + prefix.Length;
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(htmlFragment);
        int endHtml = endFragment + suffix.Length;
        string header = string.Format(System.Globalization.CultureInfo.InvariantCulture, headerFormat, headerBytes, endHtml, startFragment, endFragment);
        return Encoding.UTF8.GetBytes(header + prefix + htmlFragment + suffix);
    }

    /// <summary>
    /// Decodes a packed DIB (BITMAPINFOHEADER, V4 or V5) of 24 or 32 bpp to top-down RGBA. 32-bpp
    /// channels come from the BI_BITFIELDS masks when present (after a 40-byte header, or inside a
    /// V4/V5 header) — ignoring them used to shift the pixels by the 12 mask bytes. A 32-bpp BI_RGB
    /// image whose alpha bytes are all zero has no alpha and is read as opaque.
    /// </summary>
    internal static ImageData? DecodeDib(byte[] dib)
    {
        if (dib.Length < 40)
        {
            return null;
        }
        var d = dib.AsSpan();
        int headerSize = BitConverter.ToInt32(dib, 0);
        int width = BitConverter.ToInt32(dib, 4);
        int rawHeight = BitConverter.ToInt32(dib, 8);
        int bitCount = BitConverter.ToInt16(dib, 14);
        uint compression = BitConverter.ToUInt32(dib, 16);
        uint colorsUsed = BitConverter.ToUInt32(dib, 32);
        if (width <= 0 || width > 65536 || rawHeight == 0 || Math.Abs(rawHeight) > 65536 || (bitCount != 24 && bitCount != 32))
        {
            return null;
        }

        uint rMask = 0x00FF0000, gMask = 0x0000FF00, bMask = 0x000000FF, aMask = 0;
        int pixelOffset = headerSize + (int)colorsUsed * 4;
        bool masks = compression is 3 or 6; // BI_BITFIELDS, BI_ALPHABITFIELDS
        if (masks && headerSize >= 56)
        {
            rMask = BitConverter.ToUInt32(dib, 40);
            gMask = BitConverter.ToUInt32(dib, 44);
            bMask = BitConverter.ToUInt32(dib, 48);
            aMask = BitConverter.ToUInt32(dib, 52);
        }
        else if (masks)
        {
            rMask = BitConverter.ToUInt32(dib, headerSize);
            gMask = BitConverter.ToUInt32(dib, headerSize + 4);
            bMask = BitConverter.ToUInt32(dib, headerSize + 8);
            aMask = compression == 6 ? BitConverter.ToUInt32(dib, headerSize + 12) : 0;
            pixelOffset += compression == 6 ? 16 : 12;
        }
        else if (bitCount == 32)
        {
            aMask = 0xFF000000;
        }

        int height = Math.Abs(rawHeight);
        int bytesPerPixel = bitCount / 8;
        int srcStride = (width * bytesPerPixel + 3) & ~3;
        if (pixelOffset < 0 || pixelOffset + (long)srcStride * height > dib.Length)
        {
            return null;
        }

        byte[] rgba = new byte[width * height * 4];
        bool anyAlpha = false;
        for (int y = 0; y < height; y++)
        {
            var src = d.Slice(pixelOffset + (rawHeight > 0 ? height - 1 - y : y) * srcStride, width * bytesPerPixel);
            var dst = rgba.AsSpan(y * width * 4, width * 4);
            for (int x = 0; x < width; x++)
            {
                if (bitCount == 24)
                {
                    dst[x * 4] = src[x * 3 + 2];
                    dst[x * 4 + 1] = src[x * 3 + 1];
                    dst[x * 4 + 2] = src[x * 3];
                    dst[x * 4 + 3] = 255;
                    continue;
                }
                uint pixel = BitConverter.ToUInt32(src.Slice(x * 4, 4));
                dst[x * 4] = Channel(pixel, rMask);
                dst[x * 4 + 1] = Channel(pixel, gMask);
                dst[x * 4 + 2] = Channel(pixel, bMask);
                byte a = aMask == 0 ? (byte)255 : Channel(pixel, aMask);
                dst[x * 4 + 3] = a;
                anyAlpha |= a != 0;
            }
        }

        if (bitCount == 32 && !masks && !anyAlpha)
        {
            for (int i = 3; i < rgba.Length; i += 4)
            {
                rgba[i] = 255;
            }
        }

        return new ImageData { Pixels = rgba, Width = width, Height = height, Stride = width * 4 };
    }

    private static byte Channel(uint pixel, uint mask)
    {
        if (mask == 0)
        {
            return 0;
        }
        int shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        uint max = mask >> shift;
        uint value = (pixel & mask) >> shift;
        return max == 255 ? (byte)value : (byte)(value * 255 / max);
    }
}

/// <summary>
/// Quick check of which formats are available on the clipboard.
/// </summary>
internal record struct ClipboardAvailability
{
    internal bool HasText;
    internal bool HasHtml;
    internal bool HasRtf;
    internal bool HasFiles;
    internal bool HasImage;
}

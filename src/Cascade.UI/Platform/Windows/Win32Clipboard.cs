using System.Runtime.InteropServices;
using System.Text;

namespace Cascade.UI;

/// <summary>
/// Win32 clipboard implementation. Wraps OpenClipboard/CloseClipboard and
/// supports CF_UNICODETEXT, CF_HTML, and CF_HDROP formats.
/// </summary>
internal static class Win32Clipboard
{
    private static uint cfHtml;
    private static uint cfRtf;

    /// <summary>
    /// Registers clipboard formats. Call once during app init.
    /// </summary>
    internal static void Initialize()
    {
        cfHtml = Win32.RegisterClipboardFormatW("HTML Format");
        cfRtf = Win32.RegisterClipboardFormatW("Rich Text Format");
    }

    /// <summary>
    /// The registered clipboard format ID for HTML.
    /// </summary>
    internal static uint CfHtml => cfHtml;

    /// <summary>
    /// The registered clipboard format ID for RTF.
    /// </summary>
    internal static uint CfRtf => cfRtf;

    // ── Reading ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns which standard formats are currently available on the clipboard.
    /// </summary>
    internal static ClipboardAvailability GetAvailableFormats()
    {
        return new ClipboardAvailability
        {
            HasText = Win32.IsClipboardFormatAvailable(Win32.CF_UNICODETEXT),
            HasHtml = cfHtml != 0 && Win32.IsClipboardFormatAvailable(cfHtml),
            HasRtf  = cfRtf != 0 && Win32.IsClipboardFormatAvailable(cfRtf),
            HasFiles = Win32.IsClipboardFormatAvailable(Win32.CF_HDROP),
            HasImage = Win32.IsClipboardFormatAvailable(Win32.CF_DIBV5) ||
                       Win32.IsClipboardFormatAvailable(Win32.CF_DIB)
        };
    }

    /// <summary>
    /// Reads plain text (CF_UNICODETEXT) from the clipboard.
    /// The clipboard must not be open when calling this method.
    /// </summary>
    internal static string? GetText()
    {
        if (!Win32.OpenClipboard(0))
        {
            return null;
        }

        try
        {
            nint hData = Win32.GetClipboardData(Win32.CF_UNICODETEXT);
            if (hData == 0)
            {
                return null;
            }

            nint pData = Win32.GlobalLock(hData);
            if (pData == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pData);
            }
            finally
            {
                Win32.GlobalUnlock(hData);
            }
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Reads HTML content from the clipboard (CF_HTML format).
    /// Parses the CF_HTML header to extract only the HTML fragment.
    /// </summary>
    internal static string? GetHtml()
    {
        if (cfHtml == 0)
        {
            return null;
        }

        if (!Win32.OpenClipboard(0))
        {
            return null;
        }

        try
        {
            nint hData = Win32.GetClipboardData(cfHtml);
            if (hData == 0)
            {
                return null;
            }

            nint pData = Win32.GlobalLock(hData);
            if (pData == 0)
            {
                return null;
            }

            try
            {
                nuint size = Win32.GlobalSize(hData);
                if (size == 0)
                {
                    return null;
                }

                byte[] buffer = new byte[(int)size];
                Marshal.Copy(pData, buffer, 0, buffer.Length);
                string fullContent = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                // Parse CF_HTML header to find fragment boundaries.
                return ExtractHtmlFragment(fullContent);
            }
            finally
            {
                Win32.GlobalUnlock(hData);
            }
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Reads file paths from CF_HDROP clipboard data.
    /// </summary>
    internal static IReadOnlyList<string>? GetFiles()
    {
        if (!Win32.OpenClipboard(0))
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
            if (fileCount == 0)
            {
                return [];
            }

            List<string> files = new((int)fileCount);
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
    /// Returns the current clipboard sequence number for change detection.
    /// </summary>
    internal static uint GetSequenceNumber()
    {
        return Win32.GetClipboardSequenceNumber();
    }

    /// <summary>
    /// Reads image data from the clipboard (CF_DIBV5 or CF_DIB).
    /// Returns RGBA pixel data with top-down row order.
    /// </summary>
    internal static ImageData? GetImage()
    {
        if (!Win32.OpenClipboard(0))
        {
            return null;
        }

        try
        {
            // Prefer CF_DIBV5 over CF_DIB for alpha channel support.
            uint format = Win32.IsClipboardFormatAvailable(Win32.CF_DIBV5) ? Win32.CF_DIBV5 : Win32.CF_DIB;
            nint hData = Win32.GetClipboardData(format);
            if (hData == 0)
            {
                return null;
            }

            nint pData = Win32.GlobalLock(hData);
            if (pData == 0)
            {
                return null;
            }

            try
            {
                return DecodeDib(pData, format == Win32.CF_DIBV5);
            }
            finally
            {
                Win32.GlobalUnlock(hData);
            }
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Reads RTF content from the clipboard.
    /// </summary>
    internal static string? GetRtf()
    {
        if (cfRtf == 0)
        {
            return null;
        }

        if (!Win32.OpenClipboard(0))
        {
            return null;
        }

        try
        {
            nint hData = Win32.GetClipboardData(cfRtf);
            if (hData == 0)
            {
                return null;
            }

            nint pData = Win32.GlobalLock(hData);
            if (pData == 0)
            {
                return null;
            }

            try
            {
                nuint size = Win32.GlobalSize(hData);
                if (size == 0)
                {
                    return null;
                }

                byte[] buffer = new byte[(int)size];
                Marshal.Copy(pData, buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            }
            finally
            {
                Win32.GlobalUnlock(hData);
            }
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    // ── Writing ──────────────────────────────────────────────────────

    /// <summary>
    /// Writes plain text to the clipboard.
    /// </summary>
    internal static bool SetText(string text)
    {
        if (!Win32.OpenClipboard(0))
        {
            return false;
        }

        try
        {
            Win32.EmptyClipboard();

            int byteCount = (text.Length + 1) * sizeof(char);
            nint hGlobal = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (nuint)byteCount);
            if (hGlobal == 0)
            {
                return false;
            }

            nint pGlobal = Win32.GlobalLock(hGlobal);
            if (pGlobal == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
                // Write null terminator.
                Marshal.WriteInt16(pGlobal + text.Length * sizeof(char), 0);
            }
            finally
            {
                Win32.GlobalUnlock(hGlobal);
            }

            nint result = Win32.SetClipboardData(Win32.CF_UNICODETEXT, hGlobal);
            if (result == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            return true;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Writes HTML content to the clipboard in CF_HTML format.
    /// Also writes the plain text form as CF_UNICODETEXT.
    /// </summary>
    internal static bool SetHtml(string html, string? plainTextFallback = null)
    {
        if (cfHtml == 0)
        {
            return false;
        }

        if (!Win32.OpenClipboard(0))
        {
            return false;
        }

        try
        {
            Win32.EmptyClipboard();

            // Build CF_HTML formatted content.
            string cfHtmlContent = BuildCfHtml(html);
            byte[] htmlBytes = Encoding.UTF8.GetBytes(cfHtmlContent);

            nint hHtml = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (nuint)(htmlBytes.Length + 1));
            if (hHtml == 0)
            {
                return false;
            }

            nint pHtml = Win32.GlobalLock(hHtml);
            if (pHtml == 0)
            {
                Win32.GlobalFree(hHtml);
                return false;
            }

            try
            {
                Marshal.Copy(htmlBytes, 0, pHtml, htmlBytes.Length);
                Marshal.WriteByte(pHtml + htmlBytes.Length, 0);
            }
            finally
            {
                Win32.GlobalUnlock(hHtml);
            }

            if (Win32.SetClipboardData(cfHtml, hHtml) == 0)
            {
                Win32.GlobalFree(hHtml);
                return false;
            }

            // Also write plain text fallback.
            if (plainTextFallback is not null)
            {
                SetClipboardTextData(plainTextFallback);
            }

            return true;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Writes file paths to the clipboard as CF_HDROP.
    /// </summary>
    internal static bool SetFiles(IReadOnlyList<string> filePaths)
    {
        if (!Win32.OpenClipboard(0))
        {
            return false;
        }

        try
        {
            Win32.EmptyClipboard();

            // Build DROPFILES structure followed by null-terminated file paths
            // and a final double null terminator.
            int headerSize = Marshal.SizeOf<Win32.DROPFILES>();

            // Align to the next multiple of sizeof(char).
            if (headerSize % sizeof(char) != 0)
            {
                headerSize += sizeof(char) - (headerSize % sizeof(char));
            }

            int totalChars = 0;
            foreach (string path in filePaths)
            {
                totalChars += path.Length + 1; // path + null terminator
            }
            totalChars += 1; // final double null

            int totalSize = headerSize + totalChars * sizeof(char);
            nint hGlobal = Win32.GlobalAlloc(Win32.GHND, (nuint)totalSize);
            if (hGlobal == 0)
            {
                return false;
            }

            nint pGlobal = Win32.GlobalLock(hGlobal);
            if (pGlobal == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            try
            {
                // Write DROPFILES header.
                Win32.DROPFILES dropFiles = new()
                {
                    pFiles = (uint)headerSize,
                    fWide = 1 // Unicode
                };
                Marshal.StructureToPtr(dropFiles, pGlobal, false);

                // Write file paths.
                nint pCurrent = pGlobal + headerSize;
                foreach (string path in filePaths)
                {
                    char[] chars = path.ToCharArray();
                    Marshal.Copy(chars, 0, pCurrent, chars.Length);
                    pCurrent += chars.Length * sizeof(char);
                    Marshal.WriteInt16(pCurrent, 0); // null terminator
                    pCurrent += sizeof(char);
                }

                // Final double null is already zero (GHND zeroes memory).
            }
            finally
            {
                Win32.GlobalUnlock(hGlobal);
            }

            nint result = Win32.SetClipboardData(Win32.CF_HDROP, hGlobal);
            if (result == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            return true;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    // ── Monitoring ───────────────────────────────────────────────────

    /// <summary>
    /// Registers a window to receive WM_CLIPBOARDUPDATE messages.
    /// </summary>
    internal static void StartMonitoring(nint hWnd)
    {
        Win32.AddClipboardFormatListener(hWnd);
    }

    /// <summary>
    /// Unregisters a window from clipboard change notifications.
    /// </summary>
    internal static void StopMonitoring(nint hWnd)
    {
        Win32.RemoveClipboardFormatListener(hWnd);
    }

    // ── Private Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Extracts the HTML fragment from a CF_HTML formatted string.
    /// CF_HTML has headers like StartFragment:XXXX and EndFragment:XXXX.
    /// </summary>
    private static string ExtractHtmlFragment(string cfHtmlContent)
    {
        int startFragment = -1;
        int endFragment = -1;

        const string startTag = "StartFragment:";
        const string endTag = "EndFragment:";

        int startIdx = cfHtmlContent.IndexOf(startTag, StringComparison.Ordinal);
        if (startIdx >= 0)
        {
            startIdx += startTag.Length;
            int endOfLine = cfHtmlContent.IndexOf('\n', startIdx);
            if (endOfLine > startIdx)
            {
                string value = cfHtmlContent[startIdx..endOfLine].Trim('\r', ' ');
                _ = int.TryParse(value, out startFragment);
            }
        }

        int endIdx = cfHtmlContent.IndexOf(endTag, StringComparison.Ordinal);
        if (endIdx >= 0)
        {
            endIdx += endTag.Length;
            int endOfLine = cfHtmlContent.IndexOf('\n', endIdx);
            if (endOfLine < 0)
            {
                endOfLine = cfHtmlContent.Length;
            }

            string value = cfHtmlContent[endIdx..endOfLine].Trim('\r', ' ');
            _ = int.TryParse(value, out endFragment);
        }

        if (startFragment >= 0 && endFragment > startFragment && endFragment <= cfHtmlContent.Length)
        {
            return cfHtmlContent[startFragment..endFragment];
        }

        return cfHtmlContent;
    }

    /// <summary>
    /// Builds CF_HTML formatted string from an HTML fragment.
    /// </summary>
    private static string BuildCfHtml(string htmlFragment)
    {
        // CF_HTML format uses byte offsets, so we compute the header size first.
        const string header = "Version:0.9\r\n" +
                              "StartHTML:{0:D10}\r\n" +
                              "EndHTML:{1:D10}\r\n" +
                              "StartFragment:{2:D10}\r\n" +
                              "EndFragment:{3:D10}\r\n";

        const string startHtml = "<html><body>\r\n<!--StartFragment-->";
        const string endHtml = "<!--EndFragment-->\r\n</body></html>";

        // Calculate byte offsets.
        string headerTemplate = string.Format(System.Globalization.CultureInfo.InvariantCulture, header, 0, 0, 0, 0);
        int headerBytes = Encoding.UTF8.GetByteCount(headerTemplate);
        int startHtmlBytes = Encoding.UTF8.GetByteCount(startHtml);
        int fragmentBytes = Encoding.UTF8.GetByteCount(htmlFragment);
        int endHtmlBytes = Encoding.UTF8.GetByteCount(endHtml);

        int startHtmlOffset = headerBytes;
        int startFragmentOffset = headerBytes + startHtmlBytes;
        int endFragmentOffset = startFragmentOffset + fragmentBytes;
        int endHtmlOffset = endFragmentOffset + endHtmlBytes;

        string finalHeader = string.Format(System.Globalization.CultureInfo.InvariantCulture, header,
            startHtmlOffset, endHtmlOffset,
            startFragmentOffset, endFragmentOffset);

        return finalHeader + startHtml + htmlFragment + endHtml;
    }

    /// <summary>
    /// Writes text as CF_UNICODETEXT without opening/closing the clipboard.
    /// The clipboard must already be open.
    /// </summary>
    private static void SetClipboardTextData(string text)
    {
        int byteCount = (text.Length + 1) * sizeof(char);
        nint hGlobal = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (nuint)byteCount);
        if (hGlobal == 0)
        {
            return;
        }

        nint pGlobal = Win32.GlobalLock(hGlobal);
        if (pGlobal == 0)
        {
            Win32.GlobalFree(hGlobal);
            return;
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
            Marshal.WriteInt16(pGlobal + text.Length * sizeof(char), 0);
        }
        finally
        {
            Win32.GlobalUnlock(hGlobal);
        }

        if (Win32.SetClipboardData(Win32.CF_UNICODETEXT, hGlobal) == 0)
        {
            Win32.GlobalFree(hGlobal);
        }
    }

    /// <summary>
    /// Writes RGBA image data to the clipboard as CF_DIBV5.
    /// The image data must be in RGBA format with 4 bytes per pixel.
    /// </summary>
    internal static bool SetImage(ImageData image)
    {
        if (image.Pixels.Length == 0 || image.Width <= 0 || image.Height <= 0)
        {
            return false;
        }

        if (!Win32.OpenClipboard(0))
        {
            return false;
        }

        try
        {
            Win32.EmptyClipboard();

            // Build a BITMAPINFOHEADER (40 bytes) + bottom-up BGRA pixel data.
            const int headerSize = 40;
            int rowBytes = image.Width * 4;
            int pixelDataSize = rowBytes * image.Height;
            int totalSize = headerSize + pixelDataSize;

            nint hGlobal = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (nuint)totalSize);
            if (hGlobal == 0)
            {
                return false;
            }

            nint pGlobal = Win32.GlobalLock(hGlobal);
            if (pGlobal == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            try
            {
                // BITMAPINFOHEADER
                Marshal.WriteInt32(pGlobal + 0, headerSize);       // biSize
                Marshal.WriteInt32(pGlobal + 4, image.Width);      // biWidth
                Marshal.WriteInt32(pGlobal + 8, image.Height);     // biHeight (positive = bottom-up)
                Marshal.WriteInt16(pGlobal + 12, 1);               // biPlanes
                Marshal.WriteInt16(pGlobal + 14, 32);              // biBitCount
                Marshal.WriteInt32(pGlobal + 16, 0);               // biCompression = BI_RGB
                Marshal.WriteInt32(pGlobal + 20, pixelDataSize);   // biSizeImage
                Marshal.WriteInt32(pGlobal + 24, 0);               // biXPelsPerMeter
                Marshal.WriteInt32(pGlobal + 28, 0);               // biYPelsPerMeter
                Marshal.WriteInt32(pGlobal + 32, 0);               // biClrUsed
                Marshal.WriteInt32(pGlobal + 36, 0);               // biClrImportant

                // Write pixel data: convert RGBA top-down to BGRA bottom-up.
                int srcStride = image.Stride > 0 ? image.Stride : image.Width * 4;
                for (int y = 0; y < image.Height; y++)
                {
                    int srcRow = y;
                    int dstRow = image.Height - 1 - y;
                    int srcOffset = srcRow * srcStride;
                    nint dstPtr = pGlobal + headerSize + dstRow * rowBytes;

                    for (int x = 0; x < image.Width; x++)
                    {
                        int si = srcOffset + x * 4;
                        byte r = image.Pixels[si + 0];
                        byte g = image.Pixels[si + 1];
                        byte b = image.Pixels[si + 2];
                        byte a = image.Pixels[si + 3];
                        Marshal.WriteByte(dstPtr + x * 4 + 0, b);
                        Marshal.WriteByte(dstPtr + x * 4 + 1, g);
                        Marshal.WriteByte(dstPtr + x * 4 + 2, r);
                        Marshal.WriteByte(dstPtr + x * 4 + 3, a);
                    }
                }
            }
            finally
            {
                Win32.GlobalUnlock(hGlobal);
            }

            nint result = Win32.SetClipboardData(Win32.CF_DIB, hGlobal);
            if (result == 0)
            {
                Win32.GlobalFree(hGlobal);
                return false;
            }

            return true;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    /// <summary>
    /// Decodes a BITMAPINFOHEADER or BITMAPV5HEADER and its pixel data into RGBA ImageData.
    /// The pointer must be locked clipboard data pointing to the DIB header.
    /// </summary>
    private static unsafe ImageData? DecodeDib(nint pData, bool isV5)
    {
        // Both BITMAPINFOHEADER and BITMAPV5HEADER start with the same fields.
        int biSize = Marshal.ReadInt32(pData + 0);
        int biWidth = Marshal.ReadInt32(pData + 4);
        int biHeight = Marshal.ReadInt32(pData + 8);
        short biBitCount = Marshal.ReadInt16(pData + 14);

        if (biWidth <= 0 || biWidth > 65536 || biHeight == 0 || Math.Abs(biHeight) > 65536)
        {
            return null;
        }

        // Only support 24-bit and 32-bit DIBs.
        if (biBitCount != 24 && biBitCount != 32)
        {
            return null;
        }

        bool bottomUp = biHeight > 0;
        int height = Math.Abs(biHeight);
        int bytesPerPixel = biBitCount / 8;
        // DIB rows are padded to 4-byte boundaries.
        int srcStride = ((biWidth * bytesPerPixel + 3) / 4) * 4;

        nint pPixels = pData + biSize;
        byte[] rgba = new byte[biWidth * height * 4];
        int dstStride = biWidth * 4;

        for (int y = 0; y < height; y++)
        {
            int srcRow = bottomUp ? (height - 1 - y) : y;
            nint srcPtr = pPixels + srcRow * srcStride;
            int dstOffset = y * dstStride;

            for (int x = 0; x < biWidth; x++)
            {
                int si = x * bytesPerPixel;
                byte b = Marshal.ReadByte(srcPtr + si + 0);
                byte g = Marshal.ReadByte(srcPtr + si + 1);
                byte r = Marshal.ReadByte(srcPtr + si + 2);
                byte a = biBitCount == 32 ? Marshal.ReadByte(srcPtr + si + 3) : (byte)255;

                rgba[dstOffset + x * 4 + 0] = r;
                rgba[dstOffset + x * 4 + 1] = g;
                rgba[dstOffset + x * 4 + 2] = b;
                rgba[dstOffset + x * 4 + 3] = a;
            }
        }

        return new ImageData
        {
            Pixels = rgba,
            Width = biWidth,
            Height = height,
            Stride = dstStride,
        };
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

namespace Cascade.UI;

/// <summary>
/// macOS clipboard implementation using NSPasteboard. Supports reading and
/// writing text, HTML, RTF, file URLs, and image data. Clipboard monitoring
/// uses NSPasteboard.changeCount polling as there is no notification-based
/// mechanism on macOS.
/// </summary>
internal static class CocoaClipboard
{
    private static long lastChangeCount;

    /// <summary>
    /// Initializes the clipboard subsystem. Reads the initial change count
    /// from NSPasteboard for monitoring purposes.
    /// </summary>
    internal static void Initialize()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard != 0)
        {
            lastChangeCount = ObjC.MsgSendLong(pasteboard, ObjC.ChangeCount);
        }
    }

    // ── Reading ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns which standard formats are currently available on the clipboard.
    /// </summary>
    internal static CocoaClipboardAvailability GetAvailableFormats()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return default;
        }

        nint types = ObjC.MsgSend(pasteboard, ObjC.Types);
        if (types == 0)
        {
            return default;
        }

        long count = ObjC.MsgSendLong(types, ObjC.Count);

        bool hasText = false;
        bool hasHtml = false;
        bool hasRtf = false;
        bool hasFiles = false;
        bool hasImage = false;

        for (long i = 0; i < count; i++)
        {
            nint typeObj = ObjC.MsgSend(types, ObjC.ObjectAtIndex, (nint)i);
            string? typeName = ObjC.FromNSString(typeObj);
            if (typeName is null)
            {
                continue;
            }

            switch (typeName)
            {
                case ObjC.NSPasteboardTypeString:
                    hasText = true;
                    break;
                case ObjC.NSPasteboardTypeHTML:
                    hasHtml = true;
                    break;
                case ObjC.NSPasteboardTypeRTF:
                    hasRtf = true;
                    break;
                case ObjC.NSPasteboardTypeFileURL:
                    hasFiles = true;
                    break;
                case ObjC.NSPasteboardTypeTIFF:
                case ObjC.NSPasteboardTypePNG:
                    hasImage = true;
                    break;
            }
        }

        return new CocoaClipboardAvailability
        {
            HasText = hasText,
            HasHtml = hasHtml,
            HasRtf = hasRtf,
            HasFiles = hasFiles,
            HasImage = hasImage
        };
    }

    /// <summary>
    /// Reads plain text from NSPasteboard.
    /// </summary>
    internal static string? GetText()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return null;
        }

        nint typeString = ObjC.ToNSString(ObjC.NSPasteboardTypeString);
        nint result = ObjC.MsgSend(pasteboard, ObjC.StringForType, typeString);
        ObjC.Release(typeString);

        return ObjC.FromNSString(result);
    }

    /// <summary>
    /// Reads HTML content from NSPasteboard.
    /// </summary>
    internal static string? GetHtml()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return null;
        }

        nint typeString = ObjC.ToNSString(ObjC.NSPasteboardTypeHTML);
        nint result = ObjC.MsgSend(pasteboard, ObjC.StringForType, typeString);
        ObjC.Release(typeString);

        return ObjC.FromNSString(result);
    }

    /// <summary>
    /// Reads RTF content from NSPasteboard.
    /// </summary>
    internal static string? GetRtf()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return null;
        }

        nint typeString = ObjC.ToNSString(ObjC.NSPasteboardTypeRTF);
        nint result = ObjC.MsgSend(pasteboard, ObjC.StringForType, typeString);
        ObjC.Release(typeString);

        return ObjC.FromNSString(result);
    }

    /// <summary>
    /// Reads file paths from NSPasteboard by reading NSURL objects.
    /// </summary>
    internal static IReadOnlyList<string>? GetFiles()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return null;
        }

        // readObjectsForClasses:options: with [NSURL.class]
        nint nsurlClass = ObjC.GetClass("NSURL");
        if (nsurlClass == 0)
        {
            return null;
        }

        nint arrayClass = ObjC.GetClass("NSArray");
        nint classArray = ObjC.MsgSend(arrayClass, ObjC.ArrayWithObject, nsurlClass);
        if (classArray == 0)
        {
            return null;
        }

        nint urls = ObjC.MsgSend(pasteboard, ObjC.ReadObjectsForClasses, classArray, 0);
        if (urls == 0)
        {
            return null;
        }

        long count = ObjC.MsgSendLong(urls, ObjC.Count);
        if (count == 0)
        {
            return [];
        }

        List<string> files = new((int)count);
        for (long i = 0; i < count; i++)
        {
            nint url = ObjC.MsgSend(urls, ObjC.ObjectAtIndex, (nint)i);
            nint path = ObjC.MsgSend(url, ObjC.Path_Sel);
            string? pathStr = ObjC.FromNSString(path);
            if (pathStr is not null)
            {
                files.Add(pathStr);
            }
        }

        return files;
    }

    /// <summary>
    /// Returns the current pasteboard change count for monitoring.
    /// </summary>
    internal static long GetChangeCount()
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return 0;
        }

        return ObjC.MsgSendLong(pasteboard, ObjC.ChangeCount);
    }

    // ── Writing ──────────────────────────────────────────────────────

    /// <summary>
    /// Writes plain text to the pasteboard.
    /// </summary>
    internal static bool SetText(string text)
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return false;
        }

        ObjC.MsgSendVoid(pasteboard, ObjC.ClearContents);

        nint nsText = ObjC.ToNSString(text);
        nint typeString = ObjC.ToNSString(ObjC.NSPasteboardTypeString);
        bool result = ObjC.MsgSendBool(pasteboard,
            ObjC.SetString_ForType, nsText, typeString);
        ObjC.Release(nsText);
        ObjC.Release(typeString);

        return result;
    }

    /// <summary>
    /// Writes HTML content to the pasteboard. Also writes plain text fallback.
    /// </summary>
    internal static bool SetHtml(string html, string? plainTextFallback = null)
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return false;
        }

        ObjC.MsgSendVoid(pasteboard, ObjC.ClearContents);

        nint nsHtml = ObjC.ToNSString(html);
        nint htmlType = ObjC.ToNSString(ObjC.NSPasteboardTypeHTML);
        bool result = ObjC.MsgSendBool(pasteboard,
            ObjC.SetString_ForType, nsHtml, htmlType);
        ObjC.Release(nsHtml);
        ObjC.Release(htmlType);

        if (result && plainTextFallback is not null)
        {
            nint nsText = ObjC.ToNSString(plainTextFallback);
            nint textType = ObjC.ToNSString(ObjC.NSPasteboardTypeString);
            ObjC.MsgSendBool(pasteboard, ObjC.SetString_ForType, nsText, textType);
            ObjC.Release(nsText);
            ObjC.Release(textType);
        }

        return result;
    }

    /// <summary>
    /// Writes file paths to the pasteboard as NSURL objects.
    /// </summary>
    internal static bool SetFiles(IReadOnlyList<string> filePaths)
    {
        nint pasteboard = GetGeneralPasteboard();
        if (pasteboard == 0)
        {
            return false;
        }

        ObjC.MsgSendVoid(pasteboard, ObjC.ClearContents);

        if (filePaths.Count == 0)
        {
            return true;
        }

        nint nsurlClass = ObjC.GetClass("NSURL");
        nint fileURLSel = ObjC.RegisterSelector("fileURLWithPath:");

        // Build an NSMutableArray of NSURL objects.
        nint mutableArrayClass = ObjC.GetClass("NSMutableArray");
        nint mutableArray = ObjC.MsgSend(mutableArrayClass, ObjC.Alloc);
        nint initWithCapSel = ObjC.RegisterSelector("initWithCapacity:");
        mutableArray = ObjC.MsgSend(mutableArray, initWithCapSel, (nint)filePaths.Count);
        nint addObjectSel = ObjC.RegisterSelector("addObject:");

        foreach (string path in filePaths)
        {
            nint nsPath = ObjC.ToNSString(path);
            nint url = ObjC.MsgSend(nsurlClass, fileURLSel, nsPath);
            ObjC.Release(nsPath);

            if (url != 0)
            {
                ObjC.MsgSendVoid(mutableArray, addObjectSel, url);
            }
        }

        bool result = ObjC.MsgSendBool(pasteboard, ObjC.WriteObjects, mutableArray);
        ObjC.Release(mutableArray);

        return result;
    }

    // ── Monitoring ───────────────────────────────────────────────────

    /// <summary>
    /// Checks if the pasteboard content has changed since the last check.
    /// Returns true and updates the stored change count if changed.
    /// This is the polling mechanism for clipboard monitoring on macOS.
    /// </summary>
    internal static bool CheckForChanges()
    {
        long currentCount = GetChangeCount();
        if (currentCount != lastChangeCount)
        {
            lastChangeCount = currentCount;
            return true;
        }

        return false;
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static nint GetGeneralPasteboard()
    {
        nint pasteboardClass = ObjC.GetClass("NSPasteboard");
        if (pasteboardClass == 0)
        {
            return 0;
        }

        return ObjC.MsgSend(pasteboardClass, ObjC.GeneralPasteboard);
    }
}

/// <summary>
/// Quick check of which formats are available on the macOS clipboard.
/// </summary>
internal record struct CocoaClipboardAvailability
{
    internal bool HasText;
    internal bool HasHtml;
    internal bool HasRtf;
    internal bool HasFiles;
    internal bool HasImage;
}

namespace Cascade.UI;

/// <summary>
/// macOS file and folder picker dialogs using NSOpenPanel and NSSavePanel.
/// These display the native Cocoa file chooser which supports sandboxed
/// app security-scoped bookmarks automatically.
/// </summary>
internal static class CocoaFilePicker
{
    /// <summary>
    /// Shows the open file dialog for selecting a single file.
    /// Must be called on the UI thread.
    /// </summary>
    internal static FilePickerResult? OpenFile(
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        nint panelClass = ObjC.GetClass("NSOpenPanel");
        nint panel = ObjC.MsgSend(panelClass, ObjC.OpenPanel);
        if (panel == 0)
        {
            return null;
        }

        ConfigureOpenPanel(panel, title, initialDirectory, allowMultiple: false);
        ApplyFilters(panel, filters);

        long response = ObjC.MsgSendLong(panel, ObjC.RunModal);
        if (response != ObjC.NSModalResponseOK)
        {
            return null;
        }

        nint url = ObjC.MsgSend(panel, ObjC.URL_Sel);
        if (url == 0)
        {
            return null;
        }

        nint path = ObjC.MsgSend(url, ObjC.Path_Sel);
        string? pathStr = ObjC.FromNSString(path);
        if (string.IsNullOrEmpty(pathStr))
        {
            return null;
        }

        return CreateResult(pathStr);
    }

    /// <summary>
    /// Shows the open file dialog for selecting multiple files.
    /// Must be called on the UI thread.
    /// </summary>
    internal static IReadOnlyList<FilePickerResult> OpenMultipleFiles(
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        nint panelClass = ObjC.GetClass("NSOpenPanel");
        nint panel = ObjC.MsgSend(panelClass, ObjC.OpenPanel);
        if (panel == 0)
        {
            return [];
        }

        ConfigureOpenPanel(panel, title, initialDirectory, allowMultiple: true);
        ApplyFilters(panel, filters);

        long response = ObjC.MsgSendLong(panel, ObjC.RunModal);
        if (response != ObjC.NSModalResponseOK)
        {
            return [];
        }

        nint urls = ObjC.MsgSend(panel, ObjC.URLs);
        if (urls == 0)
        {
            return [];
        }

        long count = ObjC.MsgSendLong(urls, ObjC.Count);
        if (count == 0)
        {
            return [];
        }

        List<FilePickerResult> results = new((int)count);
        for (long i = 0; i < count; i++)
        {
            nint url = ObjC.MsgSend(urls, ObjC.ObjectAtIndex, (nint)i);
            nint path = ObjC.MsgSend(url, ObjC.Path_Sel);
            string? pathStr = ObjC.FromNSString(path);
            if (!string.IsNullOrEmpty(pathStr))
            {
                FilePickerResult? result = CreateResult(pathStr);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Shows the save file dialog.
    /// Must be called on the UI thread.
    /// </summary>
    internal static FilePickerResult? SaveFile(
        string? title,
        string? suggestedName,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        nint panelClass = ObjC.GetClass("NSSavePanel");
        nint panel = ObjC.MsgSend(panelClass, ObjC.SavePanel);
        if (panel == 0)
        {
            return null;
        }

        ConfigureSavePanel(panel, title, suggestedName, initialDirectory);
        ApplyFilters(panel, filters);

        long response = ObjC.MsgSendLong(panel, ObjC.RunModal);
        if (response != ObjC.NSModalResponseOK)
        {
            return null;
        }

        nint url = ObjC.MsgSend(panel, ObjC.URL_Sel);
        if (url == 0)
        {
            return null;
        }

        nint path = ObjC.MsgSend(url, ObjC.Path_Sel);
        string? pathStr = ObjC.FromNSString(path);
        if (string.IsNullOrEmpty(pathStr))
        {
            return null;
        }

        return new FilePickerResult { Path = pathStr, Size = 0 };
    }

    /// <summary>
    /// Shows the folder picker dialog using NSOpenPanel configured for directories.
    /// Must be called on the UI thread.
    /// </summary>
    internal static string? OpenFolder(string? title, string? initialDirectory)
    {
        nint panelClass = ObjC.GetClass("NSOpenPanel");
        nint panel = ObjC.MsgSend(panelClass, ObjC.OpenPanel);
        if (panel == 0)
        {
            return null;
        }

        // Configure for directory selection only.
        ObjC.MsgSendVoid(panel, ObjC.SetCanChooseFiles, false);
        ObjC.MsgSendVoid(panel, ObjC.SetCanChooseDirectories, true);
        ObjC.MsgSendVoid(panel, ObjC.SetAllowsMultipleSelection, false);

        if (title is not null)
        {
            nint nsTitle = ObjC.ToNSString(title);
            ObjC.MsgSendVoid(panel, ObjC.SetMessage, nsTitle);
            ObjC.Release(nsTitle);
        }

        if (initialDirectory is not null)
        {
            SetInitialDirectory(panel, initialDirectory);
        }

        long response = ObjC.MsgSendLong(panel, ObjC.RunModal);
        if (response != ObjC.NSModalResponseOK)
        {
            return null;
        }

        nint url = ObjC.MsgSend(panel, ObjC.URL_Sel);
        if (url == 0)
        {
            return null;
        }

        nint path = ObjC.MsgSend(url, ObjC.Path_Sel);
        return ObjC.FromNSString(path);
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static void ConfigureOpenPanel(nint panel, string? title, string? initialDirectory, bool allowMultiple)
    {
        ObjC.MsgSendVoid(panel, ObjC.SetCanChooseFiles, true);
        ObjC.MsgSendVoid(panel, ObjC.SetCanChooseDirectories, false);
        ObjC.MsgSendVoid(panel, ObjC.SetAllowsMultipleSelection, allowMultiple);

        if (title is not null)
        {
            nint nsTitle = ObjC.ToNSString(title);
            ObjC.MsgSendVoid(panel, ObjC.SetMessage, nsTitle);
            ObjC.Release(nsTitle);
        }

        if (initialDirectory is not null)
        {
            SetInitialDirectory(panel, initialDirectory);
        }
    }

    private static void ConfigureSavePanel(nint panel, string? title, string? suggestedName, string? initialDirectory)
    {
        if (title is not null)
        {
            nint nsTitle = ObjC.ToNSString(title);
            ObjC.MsgSendVoid(panel, ObjC.SetMessage, nsTitle);
            ObjC.Release(nsTitle);
        }

        if (suggestedName is not null)
        {
            nint nsName = ObjC.ToNSString(suggestedName);
            ObjC.MsgSendVoid(panel, ObjC.SetNameFieldStringValue, nsName);
            ObjC.Release(nsName);
        }

        if (initialDirectory is not null)
        {
            SetInitialDirectory(panel, initialDirectory);
        }
    }

    /// <summary>
    /// Sets the initial directory URL on a panel.
    /// </summary>
    private static void SetInitialDirectory(nint panel, string directory)
    {
        nint nsurlClass = ObjC.GetClass("NSURL");
        nint fileURLSel = ObjC.RegisterSelector("fileURLWithPath:");
        nint nsPath = ObjC.ToNSString(directory);
        nint url = ObjC.MsgSend(nsurlClass, fileURLSel, nsPath);
        ObjC.Release(nsPath);

        if (url != 0)
        {
            ObjC.MsgSendVoid(panel, ObjC.SetDirectoryURL, url);
        }
    }

    /// <summary>
    /// Applies file type filters to a panel using UTType content types.
    /// On macOS 13+, we use setAllowedContentTypes: with UTType objects.
    /// </summary>
    private static void ApplyFilters(nint panel, IReadOnlyList<FileFilter>? filters)
    {
        if (filters is null or { Count: 0 })
        {
            return;
        }

        // Collect all unique extensions from all filters.
        List<string> extensions = [];
        foreach (FileFilter filter in filters)
        {
            foreach (string pattern in filter.Patterns)
            {
                // Extract extension from patterns like "*.png" or "*.jpg"
                int dotIndex = pattern.LastIndexOf('.');
                if (dotIndex >= 0 && dotIndex < pattern.Length - 1)
                {
                    string ext = pattern[(dotIndex + 1)..];
                    if (ext != "*" && !extensions.Contains(ext))
                    {
                        extensions.Add(ext);
                    }
                }
            }
        }

        if (extensions.Count == 0)
        {
            return;
        }

        // Build an NSArray of UTType objects from file extensions.
        nint utTypeClass = ObjC.GetClass("UTType");
        if (utTypeClass == 0)
        {
            return;
        }

        nint typeFromExtSel = ObjC.RegisterSelector("typeWithFilenameExtension:");
        nint mutableArrayClass = ObjC.GetClass("NSMutableArray");
        nint mutableArray = ObjC.MsgSend(mutableArrayClass, ObjC.Alloc);
        nint initWithCapSel = ObjC.RegisterSelector("initWithCapacity:");
        mutableArray = ObjC.MsgSend(mutableArray, initWithCapSel, (nint)extensions.Count);
        nint addObjectSel = ObjC.RegisterSelector("addObject:");

        foreach (string ext in extensions)
        {
            nint nsExt = ObjC.ToNSString(ext);
            nint utType = ObjC.MsgSend(utTypeClass, typeFromExtSel, nsExt);
            ObjC.Release(nsExt);

            if (utType != 0)
            {
                ObjC.MsgSendVoid(mutableArray, addObjectSel, utType);
            }
        }

        ObjC.MsgSendVoid(panel, ObjC.SetAllowedContentTypes, mutableArray);
        ObjC.Release(mutableArray);
    }

    /// <summary>
    /// Creates a FilePickerResult from a file path, reading the file size if the file exists.
    /// </summary>
    private static FilePickerResult? CreateResult(string path)
    {
        long size = 0;
        if (System.IO.File.Exists(path))
        {
            try
            {
                size = new System.IO.FileInfo(path).Length;
            }
            catch (System.IO.IOException)
            {
                // Ignore errors reading file size.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission errors reading file size.
            }
        }

        return new FilePickerResult { Path = path, Size = size };
    }
}

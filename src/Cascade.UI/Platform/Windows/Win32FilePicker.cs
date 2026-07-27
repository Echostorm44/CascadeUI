using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Win32 file and folder picker dialogs using GetOpenFileName/GetSaveFileName
/// and SHBrowseForFolder. These use the legacy common dialog APIs which are
/// NativeAOT-safe (no COM runtime reflection required).
/// </summary>
internal static class Win32FilePicker
{
    /// <summary>
    /// Shows the open file dialog for selecting a single file.
    /// Must be called on the UI thread.
    /// </summary>
    internal static FilePickerResult? OpenFile(
        nint ownerHwnd,
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        unsafe
        {
            char[] fileBuffer = new char[1024];

            string? filterString = BuildFilterString(filters);

            fixed (char* pFile = fileBuffer)
            fixed (char* pFilter = filterString)
            fixed (char* pTitle = title)
            fixed (char* pInitialDir = initialDirectory)
            {
                Win32.OPENFILENAMEW ofn = new()
                {
                    lStructSize = (uint)sizeof(Win32.OPENFILENAMEW),
                    hwndOwner = ownerHwnd,
                    lpstrFilter = pFilter,
                    lpstrFile = pFile,
                    nMaxFile = (uint)fileBuffer.Length,
                    lpstrTitle = pTitle,
                    lpstrInitialDir = pInitialDir,
                    flags = Win32.OFN_PATHMUSTEXIST | Win32.OFN_FILEMUSTEXIST | Win32.OFN_NOCHANGEDIR
                };

                if (!Win32.GetOpenFileNameW(&ofn))
                {
                    return null;
                }
            }

            string selectedPath = ExtractFirstPath(fileBuffer);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return null;
            }

            return CreateResult(selectedPath);
        }
    }

    /// <summary>
    /// Shows the open file dialog for selecting multiple files.
    /// Must be called on the UI thread.
    /// </summary>
    internal static IReadOnlyList<FilePickerResult> OpenMultipleFiles(
        nint ownerHwnd,
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        unsafe
        {
            char[] fileBuffer = new char[32768];

            string? filterString = BuildFilterString(filters);

            fixed (char* pFile = fileBuffer)
            fixed (char* pFilter = filterString)
            fixed (char* pTitle = title)
            fixed (char* pInitialDir = initialDirectory)
            {
                Win32.OPENFILENAMEW ofn = new()
                {
                    lStructSize = (uint)sizeof(Win32.OPENFILENAMEW),
                    hwndOwner = ownerHwnd,
                    lpstrFilter = pFilter,
                    lpstrFile = pFile,
                    nMaxFile = (uint)fileBuffer.Length,
                    lpstrTitle = pTitle,
                    lpstrInitialDir = pInitialDir,
                    flags = Win32.OFN_PATHMUSTEXIST | Win32.OFN_FILEMUSTEXIST
                            | Win32.OFN_ALLOWMULTISELECT | Win32.OFN_EXPLORER
                            | Win32.OFN_NOCHANGEDIR
                };

                if (!Win32.GetOpenFileNameW(&ofn))
                {
                    return [];
                }
            }

            return ExtractMultiplePaths(fileBuffer);
        }
    }

    /// <summary>
    /// Shows the save file dialog.
    /// Must be called on the UI thread.
    /// </summary>
    internal static FilePickerResult? SaveFile(
        nint ownerHwnd,
        string? title,
        string? suggestedName,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        unsafe
        {
            char[] fileBuffer = new char[1024];

            // Pre-fill with suggested name.
            if (!string.IsNullOrEmpty(suggestedName))
            {
                suggestedName.AsSpan().CopyTo(fileBuffer.AsSpan());
            }

            string? filterString = BuildFilterString(filters);

            // Extract default extension from the first filter pattern.
            string? defaultExt = null;
            if (filters is { Count: > 0 })
            {
                foreach (string pattern in filters[0].Patterns)
                {
                    int dotIndex = pattern.LastIndexOf('.');
                    if (dotIndex >= 0 && dotIndex < pattern.Length - 1)
                    {
                        string ext = pattern[(dotIndex + 1)..];
                        if (ext != "*")
                        {
                            defaultExt = ext;
                            break;
                        }
                    }
                }
            }

            fixed (char* pFile = fileBuffer)
            fixed (char* pFilter = filterString)
            fixed (char* pTitle = title)
            fixed (char* pInitialDir = initialDirectory)
            fixed (char* pDefExt = defaultExt)
            {
                Win32.OPENFILENAMEW ofn = new()
                {
                    lStructSize = (uint)sizeof(Win32.OPENFILENAMEW),
                    hwndOwner = ownerHwnd,
                    lpstrFilter = pFilter,
                    lpstrFile = pFile,
                    nMaxFile = (uint)fileBuffer.Length,
                    lpstrTitle = pTitle,
                    lpstrInitialDir = pInitialDir,
                    lpstrDefExt = pDefExt,
                    flags = Win32.OFN_PATHMUSTEXIST | Win32.OFN_OVERWRITEPROMPT | Win32.OFN_NOCHANGEDIR
                };

                if (!Win32.GetSaveFileNameW(&ofn))
                {
                    return null;
                }
            }

            string selectedPath = ExtractFirstPath(fileBuffer);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return null;
            }

            return new FilePickerResult { Path = selectedPath, Size = 0 };
        }
    }

    /// <summary>
    /// Shows the folder picker dialog using SHBrowseForFolder.
    /// Must be called on the UI thread.
    /// </summary>
    internal static string? OpenFolder(nint ownerHwnd, string? title)
    {
        nint titlePtr = 0;
        try
        {
            if (title is not null)
            {
                titlePtr = Marshal.StringToHGlobalUni(title);
            }

            Win32.BROWSEINFOW browseInfo = new()
            {
                hwndOwner = ownerHwnd,
                lpszTitle = titlePtr,
                ulFlags = Win32.BIF_RETURNONLYFSDIRS | Win32.BIF_NEWDIALOGSTYLE
            };

            nint pidl = Win32.SHBrowseForFolderW(ref browseInfo);
            if (pidl == 0)
            {
                return null;
            }

            try
            {
                char[] pathBuffer = new char[260];
                if (Win32.SHGetPathFromIDListW(pidl, pathBuffer))
                {
                    int nullIndex = Array.IndexOf(pathBuffer, '\0');
                    if (nullIndex < 0)
                    {
                        nullIndex = pathBuffer.Length;
                    }

                    return new string(pathBuffer, 0, nullIndex);
                }

                return null;
            }
            finally
            {
                Win32.CoTaskMemFree(pidl);
            }
        }
        finally
        {
            if (titlePtr != 0)
            {
                Marshal.FreeHGlobal(titlePtr);
            }
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Builds the double-null-terminated filter string used by GetOpenFileName.
    /// Format: "Label\0*.ext;*.ext2\0Label2\0*.ext3\0\0"
    /// </summary>
    private static string? BuildFilterString(IReadOnlyList<FileFilter>? filters)
    {
        if (filters is null or { Count: 0 })
        {
            return null;
        }

        // Use StringBuilder and convert nulls to actual null characters.
        System.Text.StringBuilder sb = new();
        foreach (FileFilter filter in filters)
        {
            sb.Append(filter.Label);
            sb.Append('\0');
            sb.Append(string.Join(';', filter.Patterns));
            sb.Append('\0');
        }

        sb.Append('\0'); // Final null terminator.
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the first null-terminated string from a char buffer.
    /// </summary>
    private static string ExtractFirstPath(char[] buffer)
    {
        int nullIndex = Array.IndexOf(buffer, '\0');
        if (nullIndex <= 0)
        {
            return "";
        }

        return new string(buffer, 0, nullIndex);
    }

    /// <summary>
    /// Extracts multiple file paths from an OFN_ALLOWMULTISELECT buffer.
    /// When multiple files are selected, the buffer contains:
    /// directory\0file1\0file2\0\0
    /// When a single file is selected, it's just: fullpath\0\0
    /// </summary>
#pragma warning disable CA1859 // Return concrete type for internal method
    private static List<FilePickerResult> ExtractMultiplePaths(char[] buffer)
#pragma warning restore CA1859
    {
        List<string> segments = [];
        int start = 0;

        while (start < buffer.Length)
        {
            int nullIndex = Array.IndexOf(buffer, '\0', start);
            if (nullIndex < 0 || nullIndex == start)
            {
                break;
            }

            segments.Add(new string(buffer, start, nullIndex - start));
            start = nullIndex + 1;
        }

        if (segments.Count == 0)
        {
            return [];
        }

        // Single file: the entire path is in the first segment.
        if (segments.Count == 1)
        {
            FilePickerResult? result = CreateResult(segments[0]);
            return result is not null ? [result] : [];
        }

        // Multiple files: first segment is directory, rest are file names.
        string directory = segments[0];
        List<FilePickerResult> results = new(segments.Count - 1);
        for (int i = 1; i < segments.Count; i++)
        {
            string fullPath = System.IO.Path.Combine(directory, segments[i]);
            FilePickerResult? result = CreateResult(fullPath);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
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

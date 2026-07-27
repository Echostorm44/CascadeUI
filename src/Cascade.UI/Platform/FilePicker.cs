namespace Cascade.UI;

/// <summary>
/// Provides OS-native file open and save dialogs. These are the one place
/// where the OS draws the UI — native file pickers are correct because users
/// have muscle memory for them.
/// </summary>
public static class FilePicker
{
    /// <summary>
    /// Shows the OS-native open file dialog for selecting a single file.
    /// Returns null if the user cancels.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="filters">File type filters shown in the dialog.</param>
    /// <param name="initialDirectory">The initial directory to display. Null for OS default.</param>
    public static Task<FilePickerResult?> OpenAsync(
        string? title = null,
        IReadOnlyList<FileFilter>? filters = null,
        string? initialDirectory = null)
    {
        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;

            if (Dispatcher.Loop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => Win32FilePicker.OpenFile(hwnd, title, filters, initialDirectory));
            }

            return Task.FromResult(Win32FilePicker.OpenFile(hwnd, title, filters, initialDirectory));
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (Dispatcher.CocoaLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => CocoaFilePicker.OpenFile(title, filters, initialDirectory));
            }

            return Task.FromResult(CocoaFilePicker.OpenFile(title, filters, initialDirectory));
        }
        else if (OperatingSystem.IsLinux())
        {
            if (Dispatcher.LinuxLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => LinuxFilePicker.OpenFile(title, filters, initialDirectory));
            }

            return Task.FromResult(LinuxFilePicker.OpenFile(title, filters, initialDirectory));
        }
        else
        {
            throw new PlatformNotSupportedException("FilePicker is only supported on Windows, macOS, and Linux.");
        }
    }

    /// <summary>
    /// Shows the OS-native open file dialog for selecting multiple files.
    /// Returns an empty list if the user cancels.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="filters">File type filters shown in the dialog.</param>
    /// <param name="initialDirectory">The initial directory to display. Null for OS default.</param>
    public static Task<IReadOnlyList<FilePickerResult>> OpenMultipleAsync(
        string? title = null,
        IReadOnlyList<FileFilter>? filters = null,
        string? initialDirectory = null)
    {
        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;

            if (Dispatcher.Loop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync<IReadOnlyList<FilePickerResult>>(
                    () => Win32FilePicker.OpenMultipleFiles(hwnd, title, filters, initialDirectory));
            }

            return Task.FromResult<IReadOnlyList<FilePickerResult>>(
                Win32FilePicker.OpenMultipleFiles(hwnd, title, filters, initialDirectory));
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (Dispatcher.CocoaLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync<IReadOnlyList<FilePickerResult>>(
                    () => CocoaFilePicker.OpenMultipleFiles(title, filters, initialDirectory));
            }

            return Task.FromResult<IReadOnlyList<FilePickerResult>>(
                CocoaFilePicker.OpenMultipleFiles(title, filters, initialDirectory));
        }
        else if (OperatingSystem.IsLinux())
        {
            if (Dispatcher.LinuxLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync<IReadOnlyList<FilePickerResult>>(
                    () => LinuxFilePicker.OpenMultipleFiles(title, filters, initialDirectory));
            }

            return Task.FromResult<IReadOnlyList<FilePickerResult>>(
                LinuxFilePicker.OpenMultipleFiles(title, filters, initialDirectory));
        }
        else
        {
            throw new PlatformNotSupportedException("FilePicker is only supported on Windows, macOS, and Linux.");
        }
    }

    /// <summary>
    /// Shows the OS-native save file dialog. Returns null if the user cancels.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="suggestedName">The default file name pre-filled in the dialog.</param>
    /// <param name="filters">File type filters shown in the dialog.</param>
    /// <param name="initialDirectory">The initial directory to display. Null for OS default.</param>
    public static Task<FilePickerResult?> SaveAsync(
        string? title = null,
        string? suggestedName = null,
        IReadOnlyList<FileFilter>? filters = null,
        string? initialDirectory = null)
    {
        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;

            if (Dispatcher.Loop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(
                    () => Win32FilePicker.SaveFile(hwnd, title, suggestedName, filters, initialDirectory));
            }

            return Task.FromResult(Win32FilePicker.SaveFile(hwnd, title, suggestedName, filters, initialDirectory));
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (Dispatcher.CocoaLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(
                    () => CocoaFilePicker.SaveFile(title, suggestedName, filters, initialDirectory));
            }

            return Task.FromResult(CocoaFilePicker.SaveFile(title, suggestedName, filters, initialDirectory));
        }
        else if (OperatingSystem.IsLinux())
        {
            if (Dispatcher.LinuxLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(
                    () => LinuxFilePicker.SaveFile(title, suggestedName, filters, initialDirectory));
            }

            return Task.FromResult(LinuxFilePicker.SaveFile(title, suggestedName, filters, initialDirectory));
        }
        else
        {
            throw new PlatformNotSupportedException("FilePicker is only supported on Windows, macOS, and Linux.");
        }
    }
}

namespace Cascade.UI;

/// <summary>
/// Provides an OS-native folder selection dialog.
/// </summary>
public static class FolderPicker
{
    /// <summary>
    /// Shows the OS-native folder picker dialog. Returns the selected
    /// folder's absolute path, or null if the user cancels.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="initialDirectory">The initial directory to display. Null for OS default.</param>
    public static Task<string?> OpenAsync(
        string? title = null,
        string? initialDirectory = null)
    {
        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;

            if (Dispatcher.Loop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => Win32FilePicker.OpenFolder(hwnd, title));
            }

            return Task.FromResult(Win32FilePicker.OpenFolder(hwnd, title));
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (Dispatcher.CocoaLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => CocoaFilePicker.OpenFolder(title, initialDirectory));
            }

            return Task.FromResult(CocoaFilePicker.OpenFolder(title, initialDirectory));
        }
        else if (OperatingSystem.IsLinux())
        {
            if (Dispatcher.LinuxLoop is not null && !Dispatcher.IsOnUiThread)
            {
                return Dispatcher.InvokeAsync(() => LinuxFilePicker.OpenFolder(title));
            }

            return Task.FromResult(LinuxFilePicker.OpenFolder(title));
        }
        else
        {
            throw new PlatformNotSupportedException("FolderPicker is only supported on Windows, macOS, and Linux.");
        }
    }
}

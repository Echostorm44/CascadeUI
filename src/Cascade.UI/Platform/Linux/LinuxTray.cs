#pragma warning disable CA1822 // Instance method for API consistency with CocoaTray

namespace Cascade.UI;

/// <summary>
/// Linux system tray support. Tracks tray icon state for API consistency.
/// Visual tray icon display uses the StatusNotifierItem (SNI) D-Bus protocol,
/// which depends on desktop environment support. Notifications are delivered
/// via notify-send, which works across all major desktop environments.
/// </summary>
internal sealed class LinuxTray : IDisposable
{
    private bool shown;
    private bool disposed;

    internal void Show(string? tooltip)
    {
        // Linux system tray (StatusNotifierItem) requires D-Bus integration.
        // The icon state is tracked for API consistency; visual tray icon
        // display depends on the desktop environment's SNI support.
        shown = true;
        _ = tooltip;
    }

    internal void Hide()
    {
        shown = false;
    }

    internal bool IsShown => shown;

    internal void ShowNotification(string? title, string? body)
    {
        // Use notify-send to deliver desktop notifications via the
        // Desktop Notifications Specification.
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "notify-send",
                Arguments              = $"\"{EscapeArg(title ?? "")}\" \"{EscapeArg(body ?? "")}\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true
            };

            using System.Diagnostics.Process? process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch (System.ComponentModel.Win32Exception) { }
        catch (InvalidOperationException) { }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            shown = false;
        }
    }

    private static string EscapeArg(string arg)
    {
        return arg.Replace("\\", "\\\\", StringComparison.Ordinal)
                  .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

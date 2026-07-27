namespace Cascade.UI;

/// <summary>
/// Linux OS notification support via notify-send. Works across all major
/// desktop environments (GNOME, KDE, XFCE) that implement the Desktop
/// Notifications Specification.
/// </summary>
internal static class LinuxNotifications
{
    internal static void Show(string? title, string? body)
    {
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

    private static string EscapeArg(string arg)
    {
        return arg.Replace("\\", "\\\\", StringComparison.Ordinal)
                  .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

using System.Diagnostics;
using Cascade.UI.Updater.Core;

namespace Cascade.UI.Updater.Shim;

/// <summary>
/// The tiny standalone updater/launcher. It runs in two roles, both using only
/// <see cref="Cascade.UI.Updater.Core"/> so the binary stays small:
///   • <b>on-demand helper</b> — a running app stages an update, spawns this with <c>--wait-pid</c>,
///     and exits; the helper waits for the app to exit, applies the swap, and relaunches it.
///   • <b>launcher/shim</b> — the installed entry point: it auto-rolls-back a failed update, applies
///     any staged update, then launches the real app.
/// Usage:
///   cascade-update --install-dir &lt;dir&gt; [--app &lt;exe&gt;] [--wait-pid &lt;pid&gt;] [--app-args "&lt;args&gt;"]
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? installDir = GetOption(args, "--install-dir");
        string? appExe = GetOption(args, "--app");
        string? appArgs = GetOption(args, "--app-args");
        string? waitPidText = GetOption(args, "--wait-pid");

        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
        {
            Console.Error.WriteLine("cascade-update: --install-dir <existing directory> is required.");
            return 2;
        }

        if (waitPidText is not null && int.TryParse(waitPidText, out int pid))
        {
            WaitForProcessExit(pid, TimeSpan.FromSeconds(60));
        }

        bool rollbackRequested = Array.Exists(args, a => string.Equals(a, "--rollback", StringComparison.Ordinal));

        try
        {
            if (rollbackRequested)
            {
                Console.WriteLine(UpdateSwap.Rollback(installDir)
                    ? "cascade-update: rolled back to the previous version."
                    : "cascade-update: no previous version to roll back to.");
            }
            else
            {
                if (UpdateBootstrap.DetectCrashAndRollback(installDir))
                {
                    Console.WriteLine("cascade-update: previous update did not become healthy — rolled back.");
                }
                if (UpdateBootstrap.ApplyPendingIfAny(installDir))
                {
                    Console.WriteLine("cascade-update: applied staged update.");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"cascade-update: failed to apply update: {ex.Message}");
            return 1;
        }

        if (string.IsNullOrEmpty(appExe))
        {
            return 0;
        }

        if (!File.Exists(appExe))
        {
            Console.Error.WriteLine($"cascade-update: app executable not found: {appExe}");
            return 3;
        }

        UpdateBootstrap.BeginLaunch(installDir);
        return LaunchApp(appExe, appArgs) ? 0 : 4;
    }

    private static bool LaunchApp(string appExe, string? appArgs)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = Path.GetDirectoryName(appExe) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
            };
            if (!string.IsNullOrEmpty(appArgs))
            {
                startInfo.Arguments = appArgs;
            }
            using Process? proc = Process.Start(startInfo);
            return proc is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Console.Error.WriteLine($"cascade-update: failed to launch app: {ex.Message}");
            return false;
        }
    }

    private static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        try
        {
            using Process proc = Process.GetProcessById(pid);
            proc.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Process already exited — nothing to wait for.
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}

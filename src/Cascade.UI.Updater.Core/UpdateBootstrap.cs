using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// Startup-time update orchestration used by the standalone launcher/shim: apply a staged update
/// before the app loads its own files, and detect a failed update (the previous launch never became
/// healthy) to auto-roll-back. The running app calls <see cref="MarkLaunchHealthy"/> once it is up.
/// </summary>
/// <remarks>
/// <see cref="DetectCrashAndRollback"/>, <see cref="ApplyPendingIfAny"/> and <see cref="BeginLaunch"/>
/// belong to the <em>shim</em>. An app that calls <see cref="DetectCrashAndRollback"/> at its own
/// startup is looking at the marker the shim wrote for the launch that is happening right now, and
/// would roll its own update back on the spot. The marker carries the launched process id so that
/// misuse is caught (a marker for a live process is not a crash), but the app should only ever
/// call <see cref="MarkLaunchHealthy"/>.
/// </remarks>
public static class UpdateBootstrap
{
    /// <summary>
    /// If the previous launch left an uncleared launch marker and a rollback backup exists, the
    /// updated version failed to become healthy — roll back. A stale marker with no backup is just
    /// cleared. A marker whose recorded process is still running is not a crash: it is left in
    /// place and nothing happens. Returns true if a rollback occurred.
    /// </summary>
    public static bool DetectCrashAndRollback(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string marker = Path.Combine(installDir, UpdateLayout.LaunchMarkerName);
        if (!File.Exists(marker))
        {
            return false;
        }

        if (ReadLaunchedPid(marker) is { } pid && IsProcessAlive(pid))
        {
            // The launch this marker describes is still in progress (or the app is up and simply
            // has not called MarkLaunchHealthy yet). Not our call to make.
            return false;
        }

        bool rolledBack = false;
        if (UpdateSwap.CanRollback(installDir))
        {
            rolledBack = UpdateSwap.Rollback(installDir);
        }
        DeleteQuietly(marker);
        return rolledBack;
    }

    /// <summary>Applies a staged update if one is present. Returns true if an update was applied.</summary>
    public static bool ApplyPendingIfAny(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        if (!UpdateSwap.HasStagedUpdate(installDir))
        {
            return false;
        }
        UpdateSwap.ApplyStaged(installDir);
        return true;
    }

    /// <summary>
    /// Records that a launch is starting (cleared by <see cref="MarkLaunchHealthy"/> once stable).
    /// Pass the launched process id when it is known, so a check that runs while that process is
    /// still alive does not mistake it for a crash.
    /// </summary>
    public static void BeginLaunch(string installDir, int? launchedPid = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string content = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (launchedPid is { } pid)
        {
            content += "\npid=" + pid.ToString(CultureInfo.InvariantCulture);
        }
        File.WriteAllText(Path.Combine(installDir, UpdateLayout.LaunchMarkerName), content);
    }

    /// <summary>Called by the running app once it is healthy (e.g. after the first frame) to defuse crash detection.</summary>
    public static void MarkLaunchHealthy(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        DeleteQuietly(Path.Combine(installDir, UpdateLayout.LaunchMarkerName));
    }

    private static int? ReadLaunchedPid(string marker)
    {
        try
        {
            foreach (string line in File.ReadLines(marker))
            {
                if (line.StartsWith("pid=", StringComparison.Ordinal)
                    && int.TryParse(line.AsSpan(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid))
                {
                    return pid;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;   // no such process
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

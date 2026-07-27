using System.IO;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// Startup-time update orchestration used by the standalone launcher/shim: apply a staged update
/// before the app loads its own files, and detect a failed update (the previous launch never became
/// healthy) to auto-roll-back. The running app calls <see cref="MarkLaunchHealthy"/> once it is up.
/// </summary>
public static class UpdateBootstrap
{
    /// <summary>
    /// If the previous launch left an uncleared launch marker and a rollback backup exists, the
    /// updated version failed to become healthy — roll back. A stale marker with no backup is just
    /// cleared. Returns true if a rollback occurred.
    /// </summary>
    public static bool DetectCrashAndRollback(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string marker = Path.Combine(installDir, UpdateLayout.LaunchMarkerName);
        if (!File.Exists(marker))
        {
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

    /// <summary>Records that a launch is starting (cleared by <see cref="MarkLaunchHealthy"/> once stable).</summary>
    public static void BeginLaunch(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        File.WriteAllText(Path.Combine(installDir, UpdateLayout.LaunchMarkerName), DateTimeOffset.UtcNow.ToString("O"));
    }

    /// <summary>Called by the running app once it is healthy (e.g. after the first frame) to defuse crash detection.</summary>
    public static void MarkLaunchHealthy(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        DeleteQuietly(Path.Combine(installDir, UpdateLayout.LaunchMarkerName));
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

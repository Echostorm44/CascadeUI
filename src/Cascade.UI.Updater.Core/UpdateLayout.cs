namespace Cascade.UI.Updater.Core;

/// <summary>
/// The on-disk convention shared by the in-app updater (<c>Cascade.UI.Installer</c>) and the tiny
/// standalone updater/launcher exe (<c>Cascade.UI.Updater.Shim</c>). Both sides must agree on these
/// names, so they live in one zero-dependency place that either can reference without pulling in the
/// UI/GPU stack.
/// </summary>
public static class UpdateLayout
{
    /// <summary>The install manifest written by the install engine (records what was installed).</summary>
    public const string ManifestFileName = ".cascade-install";

    /// <summary>Extracted, verified update payload waiting to be swapped in.</summary>
    public const string StagingDirName = ".cascade-staged";

    /// <summary>The previous version, kept for the rollback window after an update is applied.</summary>
    public const string BackupDirName = ".cascade-backup";

    /// <summary>Marker (JSON: target version + staged time) that a staged update is ready to apply.</summary>
    public const string PendingMarkerName = ".cascade-pending";

    /// <summary>Marker (JSON: previous version + applied time) for the rollback window.</summary>
    public const string RollbackMarkerName = ".cascade-rollback";

    /// <summary>Marker written by the launcher before starting the app, cleared once it is healthy.</summary>
    public const string LaunchMarkerName = ".cascade-launch";

    /// <summary>Scratch directory for downloaded packages.</summary>
    public const string CacheDirName = ".cascade-cache";

    /// <summary>Where rollback moves the superseded tree aside (it may hold the running shim's own files).</summary>
    public const string DiscardDirName = ".cascade-discard";

    /// <summary>Top-level names that the swap never moves (they are framework bookkeeping, not app files).</summary>
    public static IReadOnlyList<string> ReservedNames { get; } =
    [
        ManifestFileName,
        StagingDirName,
        BackupDirName,
        PendingMarkerName,
        RollbackMarkerName,
        LaunchMarkerName,
        CacheDirName,
        DiscardDirName,
    ];

    /// <summary>True when <paramref name="name"/> is a reserved bookkeeping entry (case-insensitive).</summary>
    public static bool IsReserved(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        foreach (string reserved in ReservedNames)
        {
            if (string.Equals(name, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

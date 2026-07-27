using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// Performs the on-disk update swap and its inverse, with no dependency on the UI/GPU stack so the
/// tiny standalone launcher can reuse it. The swap is reserved-name aware (see <see cref="UpdateLayout"/>),
/// restores the previous tree if a move fails part-way, and keeps the prior version for rollback.
/// Manifest edits go through <see cref="JsonNode"/> (reflection-free, AOT-safe).
/// </summary>
public static class UpdateSwap
{
    /// <summary>Extracts a downloaded full-package zip into the staging directory and records the pending version.</summary>
    public static string StageZip(string zipPath, string installDir, string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(zipPath);
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        ArgumentException.ThrowIfNullOrEmpty(version);
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Update package not found.", zipPath);
        }

        string staging = Path.Combine(installDir, UpdateLayout.StagingDirName);
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

        MarkStaged(installDir, version);
        return staging;
    }

    /// <summary>
    /// Records that the staging directory holds a ready update for <paramref name="version"/>. Used
    /// after a delta apply, which builds the staged tree directly rather than extracting a zip.
    /// </summary>
    public static void MarkStaged(string installDir, string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        ArgumentException.ThrowIfNullOrEmpty(version);
        var pending = new JsonObject
        {
            ["version"] = version,
            ["stagedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(Path.Combine(installDir, UpdateLayout.PendingMarkerName), pending.ToJsonString());
    }

    public static bool HasStagedUpdate(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string staging = Path.Combine(installDir, UpdateLayout.StagingDirName);
        return File.Exists(Path.Combine(installDir, UpdateLayout.PendingMarkerName))
            && Directory.Exists(staging)
            && Directory.EnumerateFileSystemEntries(staging).Any();
    }

    /// <summary>The version recorded in the pending marker, or null if there is no staged update.</summary>
    public static string? PendingVersion(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        return ReadMarkerString(Path.Combine(installDir, UpdateLayout.PendingMarkerName), "version");
    }

    /// <summary>
    /// Swaps the staged tree into place: the previous live files move to the backup directory, the
    /// staged files move into the install dir, the install manifest is rewritten to the staged
    /// version with the new file list, and a rollback marker is written. Restores on failure.
    /// </summary>
    public static void ApplyStaged(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        if (!HasStagedUpdate(installDir))
        {
            throw new InvalidOperationException($"No staged update found in '{installDir}'.");
        }

        string staging = Path.Combine(installDir, UpdateLayout.StagingDirName);
        string backup = Path.Combine(installDir, UpdateLayout.BackupDirName);
        string newVersion = PendingVersion(installDir) ?? "";
        string previousVersion = ReadManifestVersion(installDir) ?? "";

        // Clean up any tree a prior rollback could not delete (its shim had it locked).
        TryDeleteDir(Path.Combine(installDir, UpdateLayout.DiscardDirName));

        if (Directory.Exists(backup))
        {
            Directory.Delete(backup, recursive: true);
        }
        Directory.CreateDirectory(backup);

        var moved = new List<(string name, bool isDir)>();
        try
        {
            foreach (string entry in TopLevelEntries(installDir))
            {
                string name = Path.GetFileName(entry);
                bool isDir = Directory.Exists(entry);
                MoveEntry(entry, Path.Combine(backup, name), isDir);
                moved.Add((name, isDir));
            }

            foreach (string entry in Directory.GetFileSystemEntries(staging))
            {
                MoveEntry(entry, Path.Combine(installDir, Path.GetFileName(entry)), Directory.Exists(entry));
            }
        }
        catch
        {
            RestoreFromBackup(installDir, backup, moved);
            throw;
        }

        Directory.Delete(staging, recursive: true);
        DeleteQuietly(Path.Combine(installDir, UpdateLayout.PendingMarkerName));

        RewriteManifest(installDir, newVersion);
        WriteMarker(installDir, UpdateLayout.RollbackMarkerName, "previousVersion", previousVersion, "appliedAt");
    }

    public static bool CanRollback(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string backup = Path.Combine(installDir, UpdateLayout.BackupDirName);
        return Directory.Exists(backup) && Directory.EnumerateFileSystemEntries(backup).Any();
    }

    /// <summary>Restores the previous version from the backup directory. Returns false if none is kept.</summary>
    public static bool Rollback(string installDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        if (!CanRollback(installDir))
        {
            return false;
        }

        string backup = Path.Combine(installDir, UpdateLayout.BackupDirName);
        string discard = Path.Combine(installDir, UpdateLayout.DiscardDirName);

        // Move the current (superseded) tree aside by RENAME, not delete: the running shim executes
        // from this directory, so its own files are locked against deletion but can still be renamed.
        TryDeleteDir(discard);
        Directory.CreateDirectory(discard);
        foreach (string entry in TopLevelEntries(installDir))
        {
            MoveEntry(entry, Path.Combine(discard, Path.GetFileName(entry)), Directory.Exists(entry));
        }

        foreach (string entry in Directory.GetFileSystemEntries(backup))
        {
            MoveEntry(entry, Path.Combine(installDir, Path.GetFileName(entry)), Directory.Exists(entry));
        }
        Directory.Delete(backup, recursive: true);

        // The discarded tree may include the still-running shim's own dll; delete what we can now and
        // leave the rest for the opportunistic cleanup at the start of the next apply/rollback.
        TryDeleteDir(discard);

        string? previousVersion = ReadMarkerString(Path.Combine(installDir, UpdateLayout.RollbackMarkerName), "previousVersion");
        if (!string.IsNullOrEmpty(previousVersion))
        {
            RewriteManifest(installDir, previousVersion);
        }
        DeleteQuietly(Path.Combine(installDir, UpdateLayout.RollbackMarkerName));
        return true;
    }

    /// <summary>Discards the kept backup once <paramref name="rollbackWindow"/> has elapsed since the update.</summary>
    public static void PruneBackup(string installDir, TimeSpan rollbackWindow)
    {
        ArgumentException.ThrowIfNullOrEmpty(installDir);
        string marker = Path.Combine(installDir, UpdateLayout.RollbackMarkerName);
        string? appliedAtText = ReadMarkerString(marker, "appliedAt");
        if (appliedAtText is null || !DateTimeOffset.TryParse(appliedAtText, out DateTimeOffset appliedAt))
        {
            return;
        }
        if (DateTimeOffset.UtcNow - appliedAt < rollbackWindow)
        {
            return;
        }

        string backup = Path.Combine(installDir, UpdateLayout.BackupDirName);
        if (Directory.Exists(backup))
        {
            Directory.Delete(backup, recursive: true);
        }
        DeleteQuietly(marker);
    }

    // ── internals ────────────────────────────────────────────────────

    private static IEnumerable<string> TopLevelEntries(string installDir)
    {
        foreach (string entry in Directory.GetFileSystemEntries(installDir))
        {
            if (!UpdateLayout.IsReserved(Path.GetFileName(entry)))
            {
                yield return entry;
            }
        }
    }

    private static void RestoreFromBackup(string installDir, string backup, List<(string name, bool isDir)> moved)
    {
        foreach ((string name, _) in moved)
        {
            DeleteEntry(Path.Combine(installDir, name));
        }
        foreach ((string name, bool isDir) in moved)
        {
            string from = Path.Combine(backup, name);
            if (File.Exists(from) || Directory.Exists(from))
            {
                MoveEntry(from, Path.Combine(installDir, name), isDir);
            }
        }
        if (Directory.Exists(backup) && !Directory.EnumerateFileSystemEntries(backup).Any())
        {
            Directory.Delete(backup);
        }
    }

    private static void MoveEntry(string source, string dest, bool isDir)
    {
        if (isDir)
        {
            Directory.Move(source, dest);
        }
        else
        {
            File.Move(source, dest, overwrite: true);
        }
    }

    private static void DeleteEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            DeleteEntry(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Best-effort recursive delete of a directory; tolerates locked files (cleaned next run).</summary>
    private static void TryDeleteDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? ReadManifestVersion(string installDir)
    {
        return ReadMarkerString(Path.Combine(installDir, UpdateLayout.ManifestFileName), "Version");
    }

    /// <summary>Rewrites the install manifest's version and file list to reflect the swapped contents.</summary>
    private static void RewriteManifest(string installDir, string newVersion)
    {
        string path = Path.Combine(installDir, UpdateLayout.ManifestFileName);
        if (!File.Exists(path))
        {
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        root["Version"] = newVersion;

        var files = new JsonArray();
        foreach (string file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
        {
            if (!UpdateLayout.IsReserved(Path.GetFileName(file)) && !UpdateLayout.IsReserved(TopSegment(installDir, file)))
            {
                JsonNode value = file; // implicit string -> JsonNode (AOT-safe, unlike JsonArray.Add<T>)
                files.Add(value);
            }
        }
        root["InstalledFiles"] = files;

        File.WriteAllText(path, root.ToJsonString());
    }

    private static string TopSegment(string root, string fullPath)
    {
        string relative = Path.GetRelativePath(root, fullPath);
        int slash = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return slash < 0 ? relative : relative[..slash];
    }

    private static void WriteMarker(string installDir, string markerName, string key, string value, string timeKey)
    {
        var node = new JsonObject
        {
            [key] = value,
            [timeKey] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(Path.Combine(installDir, markerName), node.ToJsonString());
    }

    private static string? ReadMarkerString(string path, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)?[key]?.GetValue<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

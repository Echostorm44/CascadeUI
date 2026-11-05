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
    /// Swaps the staged tree into place: the previous version's files move to the backup directory, the
    /// staged files move into the install dir, the install manifest is rewritten to the staged
    /// version with the new file list, and a rollback marker is written. Restores on failure.
    /// Only what the install owns moves: the entries its manifest lists, and any the update replaces.
    /// Anything else in the folder (an app that keeps its data beside its exe) is left where it is.
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

        List<string> stagedNames = [.. Directory.GetFileSystemEntries(staging).Select(Path.GetFileName).OfType<string>()];
        HashSet<string> owned = OwnedTopLevelNames(installDir);
        owned.UnionWith(stagedNames);

        var moved = new List<(string name, bool isDir)>();
        try
        {
            foreach (string entry in TopLevelEntries(installDir))
            {
                string name = Path.GetFileName(entry);
                if (!owned.Contains(name))
                {
                    continue;
                }
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

        RewriteManifest(installDir, newVersion, stagedNames);
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
        // Only the update's own entries, and any the backup is about to take back, move aside: the app's data
        // beside them stays put.
        List<string> restoredNames = [.. Directory.GetFileSystemEntries(backup).Select(Path.GetFileName).OfType<string>()];
        HashSet<string> current = OwnedTopLevelNames(installDir);
        current.UnionWith(restoredNames);
        TryDeleteDir(discard);
        Directory.CreateDirectory(discard);
        foreach (string entry in TopLevelEntries(installDir))
        {
            if (current.Contains(Path.GetFileName(entry)))
            {
                MoveEntry(entry, Path.Combine(discard, Path.GetFileName(entry)), Directory.Exists(entry));
            }
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
        RewriteManifest(installDir, string.IsNullOrEmpty(previousVersion) ? null : previousVersion, restoredNames);
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

    /// <summary>
    /// The top-level names (files and folders) the install manifest lists files under, minus the reserved
    /// bookkeeping. Empty when there is no readable manifest, so nothing unknown is ever treated as owned.
    /// </summary>
    internal static HashSet<string> OwnedTopLevelNames(string installDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string path = Path.Combine(installDir, UpdateLayout.ManifestFileName);
        if (!File.Exists(path))
        {
            return names;
        }

        JsonArray? files;
        try
        {
            files = (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)?["InstalledFiles"] as JsonArray;
        }
        catch (System.Text.Json.JsonException)
        {
            return names;
        }

        foreach (JsonNode? file in files ?? [])
        {
            if (file?.GetValue<string>() is not { Length: > 0 } fullPath)
            {
                continue;
            }
            string relative = Path.GetRelativePath(installDir, fullPath);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }
            string top = TopSegment(installDir, fullPath);
            if (!UpdateLayout.IsReserved(top))
            {
                names.Add(top);
            }
        }
        return names;
    }

    /// <summary>
    /// Rewrites the install manifest's version (unless null) and its file list: the reserved tools it already
    /// lists, plus every file now under <paramref name="topLevelNames"/>. Files the app keeps beside its exe are
    /// never listed, so the uninstaller never deletes them.
    /// </summary>
    private static void RewriteManifest(string installDir, string? newVersion, IReadOnlyCollection<string> topLevelNames)
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

        if (newVersion is not null)
        {
            root["Version"] = newVersion;
        }

        var files = new JsonArray();
        // The installer's own tools (the update shim, the uninstaller) are reserved and never swapped: keep them listed.
        if (root["InstalledFiles"] is JsonArray previous)
        {
            foreach (JsonNode? file in previous)
            {
                if (file?.GetValue<string>() is { Length: > 0 } fullPath && UpdateLayout.IsReserved(TopSegment(installDir, fullPath)))
                {
                    JsonNode value = fullPath; // implicit string -> JsonNode (AOT-safe, unlike JsonArray.Add<T>)
                    files.Add(value);
                }
            }
        }
        foreach (string name in topLevelNames)
        {
            string entry = Path.Combine(installDir, name);
            IEnumerable<string> under = Directory.Exists(entry)
                ? Directory.GetFiles(entry, "*", SearchOption.AllDirectories)
                : File.Exists(entry) ? [entry] : [];
            foreach (string file in under)
            {
                JsonNode value = file;
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

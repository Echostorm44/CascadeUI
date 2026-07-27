using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// A per-file delta between two published app trees. The client patches the files it already has,
/// so it never needs the original full package. The package is a zip containing a <c>delta.json</c>
/// op list (patch / add / delete) plus the bsdiff patch blobs and full bytes for added files; every
/// produced file is SHA-256 verified, so a drifted or corrupt input fails loudly (the caller then
/// falls back to a full download).
/// </summary>
public static class DeltaPackage
{
    private const string ManifestEntryName = "delta.json";

    /// <summary>Builds a delta package transforming the <paramref name="oldDir"/> tree into the <paramref name="newDir"/> tree.</summary>
    public static void Create(string oldDir, string newDir, string fromVersion, string toVersion, string outputPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(oldDir);
        ArgumentException.ThrowIfNullOrEmpty(newDir);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        Dictionary<string, string> oldFiles = RelativeFiles(oldDir);
        Dictionary<string, string> newFiles = RelativeFiles(newDir);

        var operations = new JsonArray();
        int blobIndex = 0;

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        foreach ((string rel, string newFull) in newFiles)
        {
            byte[] newBytes = File.ReadAllBytes(newFull);
            string newSha = Hashing.Sha256Hex(newBytes);

            if (oldFiles.TryGetValue(rel, out string? oldFull))
            {
                byte[] oldBytes = File.ReadAllBytes(oldFull);
                if (Hashing.Sha256Hex(oldBytes) == newSha)
                {
                    continue; // unchanged — carried over by the copy step on apply
                }

                string entry = "b/" + blobIndex++;
                WriteBlob(zip, entry, BinaryDelta.Create(oldBytes, newBytes), CompressionLevel.NoCompression);
                operations.Add((JsonNode)new JsonObject
                {
                    ["op"] = "patch",
                    ["path"] = rel,
                    ["entry"] = entry,
                    ["sha256"] = newSha,
                    ["oldSha256"] = Hashing.Sha256Hex(oldBytes),
                });
            }
            else
            {
                string entry = "b/" + blobIndex++;
                WriteBlob(zip, entry, newBytes, CompressionLevel.Optimal);
                operations.Add((JsonNode)new JsonObject
                {
                    ["op"] = "add",
                    ["path"] = rel,
                    ["entry"] = entry,
                    ["sha256"] = newSha,
                });
            }
        }

        foreach (string rel in oldFiles.Keys)
        {
            if (!newFiles.ContainsKey(rel))
            {
                operations.Add((JsonNode)new JsonObject { ["op"] = "delete", ["path"] = rel });
            }
        }

        var manifest = new JsonObject
        {
            ["fromVersion"] = fromVersion,
            ["toVersion"] = toVersion,
            ["operations"] = operations,
        };
        WriteBlob(zip, ManifestEntryName, Encoding.UTF8.GetBytes(manifest.ToJsonString()), CompressionLevel.Optimal);
    }

    /// <summary>
    /// Applies a delta package against the <paramref name="currentDir"/> install tree, producing the
    /// complete new tree in <paramref name="stagingDir"/>. Throws on drift/corruption (verified by
    /// SHA-256) so the caller can fall back to a full download.
    /// </summary>
    public static void Apply(string currentDir, string deltaPackagePath, string stagingDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentDir);
        ArgumentException.ThrowIfNullOrEmpty(deltaPackagePath);
        ArgumentException.ThrowIfNullOrEmpty(stagingDir);
        if (!File.Exists(deltaPackagePath))
        {
            throw new FileNotFoundException("Delta package not found.", deltaPackagePath);
        }

        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        Directory.CreateDirectory(stagingDir);

        // Start from a copy of the current app files (unchanged files are carried over for free).
        foreach ((string rel, string full) in RelativeFiles(currentDir))
        {
            string dest = Path.Combine(stagingDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(full, dest, overwrite: true);
        }

        using ZipArchive zip = ZipFile.OpenRead(deltaPackagePath);
        JsonObject manifest = ReadManifest(zip);
        var operations = manifest["operations"] as JsonArray
            ?? throw new InvalidDataException("Delta package manifest has no operations.");

        foreach (JsonNode? opNode in operations)
        {
            if (opNode is not JsonObject op)
            {
                continue;
            }

            string kind = op["op"]?.GetValue<string>() ?? "";
            string rel = op["path"]?.GetValue<string>() ?? throw new InvalidDataException("Delta operation missing path.");
            string stagedPath = Path.Combine(stagingDir, rel);

            switch (kind)
            {
                case "patch":
                {
                    string currentPath = Path.Combine(currentDir, rel);
                    if (!File.Exists(currentPath))
                    {
                        throw new InvalidDataException($"Delta patch references a missing file: {rel}");
                    }
                    byte[] oldBytes = File.ReadAllBytes(currentPath);
                    string? expectedOld = op["oldSha256"]?.GetValue<string>();
                    if (expectedOld is not null && !string.Equals(Hashing.Sha256Hex(oldBytes), expectedOld, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Delta patch base file drifted: {rel}");
                    }
                    byte[] patch = ReadBlob(zip, op["entry"]?.GetValue<string>());
                    byte[] newBytes = BinaryDelta.Apply(oldBytes, patch);
                    VerifyAndWrite(stagedPath, newBytes, op["sha256"]?.GetValue<string>(), rel);
                    break;
                }

                case "add":
                {
                    byte[] newBytes = ReadBlob(zip, op["entry"]?.GetValue<string>());
                    VerifyAndWrite(stagedPath, newBytes, op["sha256"]?.GetValue<string>(), rel);
                    break;
                }

                case "delete":
                {
                    if (File.Exists(stagedPath))
                    {
                        File.Delete(stagedPath);
                    }
                    break;
                }

                default:
                    throw new InvalidDataException($"Unknown delta operation: {kind}");
            }
        }
    }

    private static void VerifyAndWrite(string path, byte[] bytes, string? expectedSha, string rel)
    {
        if (expectedSha is not null && !string.Equals(Hashing.Sha256Hex(bytes), expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Delta produced a file failing SHA-256 verification: {rel}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static Dictionary<string, string> RelativeFiles(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
        {
            return map;
        }
        foreach (string full in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, full);
            string top = TopSegment(rel);
            if (UpdateLayout.IsReserved(top) || UpdateLayout.IsReserved(Path.GetFileName(full)))
            {
                continue;
            }
            map[rel.Replace('\\', '/')] = full;
        }
        return map;
    }

    private static string TopSegment(string relative)
    {
        int slash = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return slash < 0 ? relative : relative[..slash];
    }

    private static void WriteBlob(ZipArchive zip, string entryName, byte[] data, CompressionLevel level)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, level);
        using Stream stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static byte[] ReadBlob(ZipArchive zip, string? entryName)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            throw new InvalidDataException("Delta operation missing blob entry.");
        }
        ZipArchiveEntry entry = zip.GetEntry(entryName)
            ?? throw new InvalidDataException($"Delta package missing blob: {entryName}");
        using Stream stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static JsonObject ReadManifest(ZipArchive zip)
    {
        ZipArchiveEntry entry = zip.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Delta package missing delta.json.");
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd()) as JsonObject
            ?? throw new InvalidDataException("Delta package delta.json is not a JSON object.");
    }
}

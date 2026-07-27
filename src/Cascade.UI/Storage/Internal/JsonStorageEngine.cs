using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Cascade.UI;

/// <summary>
/// JSON file-backed storage engine. All key-value pairs are stored in a single
/// JSON file at a platform-appropriate location. Uses an in-memory cache for
/// fast reads and <see cref="AtomicFileWriter"/> for crash-safe writes.
/// </summary>
internal sealed class JsonStorageEngine : IStorageEngine
{
    private readonly Lock syncLock = new();
    private readonly string filePath;
    private Dictionary<string, JsonElement>? cache;

    internal JsonStorageEngine()
        : this(GetDefaultFilePath())
    {
    }

    internal JsonStorageEngine(string filePath)
    {
        this.filePath = filePath;
    }

    public string? Read(string key)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            if (cache!.TryGetValue(key, out var element))
            {
                return element.GetRawText();
            }

            return null;
        }
    }

    public void Write(string key, string json)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            using var document = JsonDocument.Parse(json);
            cache![key] = document.RootElement.Clone();
            FlushToDisk();
        }
    }

    public void Remove(string key)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            if (cache!.Remove(key))
            {
                FlushToDisk();
            }
        }
    }

    public bool Exists(string key)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            return cache!.ContainsKey(key);
        }
    }

    public void Clear()
    {
        lock (syncLock)
        {
            cache = new Dictionary<string, JsonElement>();
            FlushToDisk();
        }
    }

    public void ClearPrefix(string prefix)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            var keysToRemove = cache!.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                FlushToDisk();
            }
        }
    }

    public IReadOnlyList<string> Keys(string? prefix)
    {
        lock (syncLock)
        {
            EnsureLoaded();
            if (prefix is null)
            {
                return cache!.Keys.ToList();
            }

            return cache!.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
        }
    }

    private void EnsureLoaded()
    {
        if (cache is not null)
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            cache = new Dictionary<string, JsonElement>();
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(json);
            cache = new Dictionary<string, JsonElement>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                cache[property.Name] = property.Value.Clone();
            }
        }
        catch (JsonException)
        {
            cache = new Dictionary<string, JsonElement>();
        }
        catch (IOException)
        {
            cache = new Dictionary<string, JsonElement>();
        }
    }

    private static readonly JsonSerializerOptions flushOptions = new() { WriteIndented = true };

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Storage engine serializes Dictionary<string, JsonElement> which is always safe.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Storage engine serializes Dictionary<string, JsonElement> which is always safe.")]
    private void FlushToDisk()
    {
        using var writer = new AtomicFileWriter(filePath);
        JsonSerializer.Serialize(writer.Stream, cache!, flushOptions);
        writer.Commit();
    }

    private static string GetDefaultFilePath()
    {
        string baseDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else
        {
            baseDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local",
                    "share");
        }

        return System.IO.Path.Combine(baseDir, "CascadeUI", "storage.json");
    }
}

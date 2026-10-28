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
    private readonly string? legacyFilePath;
    private Dictionary<string, JsonElement>? cache;

    internal JsonStorageEngine()
        : this(GetDefaultFilePath(), GetLegacySharedFilePath())
    {
    }

    /// <param name="filePath">Where this app's values live.</param>
    /// <param name="legacyFilePath">
    /// Read once, when <paramref name="filePath"/> does not exist yet, so an app keeps what it stored before
    /// storage was per app. Never written or deleted (other apps may still migrate from it).
    /// </param>
    internal JsonStorageEngine(string filePath, string? legacyFilePath = null)
    {
        this.filePath = filePath;
        this.legacyFilePath = legacyFilePath;
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

        string source = filePath;
        if (!File.Exists(filePath))
        {
            if (legacyFilePath is null || !File.Exists(legacyFilePath))
            {
                cache = new Dictionary<string, JsonElement>();
                return;
            }
            // First run since storage became per app: start from what the shared file held.
            source = legacyFilePath;
        }

        try
        {
            var json = File.ReadAllText(source);
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

    /// <summary>
    /// This app's file: <c>…/CascadeUI/&lt;app&gt;/storage.json</c> under the platform's per-user data folder, so two
    /// Cascade apps never share keys. Not the app's own folder: an uninstall or update replaces that.
    /// </summary>
    private static string GetDefaultFilePath()
    {
        string appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is { Length: > 0 } name
            ? name
            : "App";
        return System.IO.Path.Combine(GetDataRoot(), "CascadeUI", appName, "storage.json");
    }

    /// <summary>The single file every Cascade app shared before storage was per app.</summary>
    private static string GetLegacySharedFilePath() => System.IO.Path.Combine(GetDataRoot(), "CascadeUI", "storage.json");

    private static string GetDataRoot()
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

        return baseDir;
    }
}

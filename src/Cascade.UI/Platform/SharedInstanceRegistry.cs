using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UI;

/// <summary>
/// Source-generated JSON context for <see cref="InstanceEntry"/> serialization.
/// Required for NativeAOT compatibility.
/// </summary>
[JsonSerializable(typeof(List<InstanceEntry>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
internal sealed partial class InstanceRegistryJsonContext : JsonSerializerContext;

/// <summary>
/// Cross-process registry of running Cascade app instances, stored in a
/// memory-mapped file. The <c>--mcp</c> proxy reads this to discover which
/// GUI instance to connect to. The GUI app writes its entry on startup and
/// removes it on shutdown.
///
/// Shared memory name: <c>Cascade-{appId}</c>.
/// Write lock: named mutex <c>Cascade-{appId}-Lock</c>.
/// Capacity: 4 KB — enough for dozens of instances.
/// </summary>
internal sealed class SharedInstanceRegistry : IDisposable
{
    private const int Capacity = 4096;

    private readonly string mapName;
    private readonly string mutexName;
    private MemoryMappedFile? map;
    private Mutex? mutex;
    private bool disposed;

    /// <summary>
    /// Creates a registry scoped to the given application ID.
    /// </summary>
    /// <param name="appId">
    /// Machine-readable application identifier (e.g. "HelloCascade").
    /// Used to construct the shared memory and mutex names.
    /// </param>
    public SharedInstanceRegistry(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        mapName = $"Cascade-{appId}";
        mutexName = $"Cascade-{appId}-Lock";
    }

    /// <summary>
    /// Registers a running instance. Call from the GUI app after the TCP
    /// MCP listener is bound and the port is known.
    /// </summary>
    public void Register(InstanceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        WithLock(() =>
        {
            var entries = ReadEntries();

            // Remove any stale entry from a crashed previous run with the same window ID
            entries.RemoveAll(e =>
                string.Equals(e.WindowId, entry.WindowId, StringComparison.Ordinal));

            entries.Add(entry);
            WriteEntries(entries);
        });
    }

    /// <summary>
    /// Removes an instance by window ID. Call from the GUI app on shutdown.
    /// </summary>
    public void Unregister(string windowId)
    {
        WithLock(() =>
        {
            var entries = ReadEntries();
            entries.RemoveAll(e =>
                string.Equals(e.WindowId, windowId, StringComparison.Ordinal));
            WriteEntries(entries);
        });
    }

    /// <summary>
    /// Returns all registered instances. Also prunes entries whose process
    /// has exited (crashed without unregistering).
    /// </summary>
    public List<InstanceEntry> FindAll()
    {
        List<InstanceEntry> result = [];
        WithLock(() =>
        {
            var entries = ReadEntries();
            bool pruned = false;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!IsProcessAlive(entries[i].Pid))
                {
                    entries.RemoveAt(i);
                    pruned = true;
                }
            }

            if (pruned)
            {
                WriteEntries(entries);
            }

            result.AddRange(entries);
        });
        return result;
    }

    /// <summary>
    /// Finds the best target instance for an <c>--mcp</c> proxy connection,
    /// following the spec rules:
    /// <list type="bullet">
    ///   <item>If <paramref name="windowId"/> is specified: connect to that instance.</item>
    ///   <item>If one instance is running: connect to it.</item>
    ///   <item>If multiple: connect to most recently activated.</item>
    ///   <item>If none: return null (headless mode).</item>
    /// </list>
    /// </summary>
    public InstanceEntry? FindTarget(string? windowId = null)
    {
        var entries = FindAll();

        if (entries.Count == 0)
        {
            return null;
        }

        if (windowId is not null)
        {
            return entries.Find(e =>
                string.Equals(e.WindowId, windowId, StringComparison.Ordinal));
        }

        if (entries.Count == 1)
        {
            return entries[0];
        }

        // Multiple instances: pick the focused one, or most recently activated
        var focused = entries.Find(e => e.Focused);
        if (focused is not null)
        {
            return focused;
        }

        entries.Sort((a, b) => b.ActivatedAt.CompareTo(a.ActivatedAt));
        return entries[0];
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        map?.Dispose();
        mutex?.Dispose();
        map = null;
        mutex = null;
    }

    // ── Internals ─────────────────────────────────────────────────────

    private void EnsureCreated()
    {
        if (map is not null)
        {
            return;
        }

        // CreateOrOpen: the first process creates the segment, subsequent
        // processes open the existing one. Windows-only API; on other
        // platforms we fall back to file-backed memory mapping.
        if (OperatingSystem.IsWindows())
        {
            map = MemoryMappedFile.CreateOrOpen(mapName, Capacity);
        }
        else
        {
            // Unix: use a file in /tmp as the backing store
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), mapName);
            if (!File.Exists(path))
            {
                using var fs = File.Create(path);
                fs.SetLength(Capacity);
            }

            map = MemoryMappedFile.CreateFromFile(path, FileMode.OpenOrCreate, null, Capacity);
        }

        mutex = new Mutex(initiallyOwned: false, mutexName);
    }

    private void WithLock(Action action)
    {
        EnsureCreated();

        bool acquired = false;
        try
        {
            acquired = mutex!.WaitOne(TimeSpan.FromSeconds(2));
            if (!acquired)
            {
                // Timeout waiting for mutex — another process might be stuck.
                // Proceed anyway to avoid deadlocking the caller.
            }

            action();
        }
        finally
        {
            if (acquired)
            {
                mutex!.ReleaseMutex();
            }
        }
    }

    private List<InstanceEntry> ReadEntries()
    {
        using var accessor = map!.CreateViewAccessor(0, Capacity, MemoryMappedFileAccess.Read);

        // First 4 bytes: length of the JSON payload
        int length = accessor.ReadInt32(0);
        if (length <= 0 || length > Capacity - 4)
        {
            return [];
        }

        byte[] buffer = new byte[length];
        accessor.ReadArray(4, buffer, 0, length);
        string json = Encoding.UTF8.GetString(buffer);

        try
        {
            return JsonSerializer.Deserialize(json, InstanceRegistryJsonContext.Default.ListInstanceEntry) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteEntries(List<InstanceEntry> entries)
    {
        string json = JsonSerializer.Serialize(entries, InstanceRegistryJsonContext.Default.ListInstanceEntry);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        if (buffer.Length > Capacity - 4)
        {
            throw new InvalidOperationException(
                $"Instance registry exceeds {Capacity} byte capacity. Too many instances registered.");
        }

        using var accessor = map!.CreateViewAccessor(0, Capacity, MemoryMappedFileAccess.Write);
        accessor.Write(0, buffer.Length);
        accessor.WriteArray(4, buffer, 0, buffer.Length);

        // Zero out any remaining bytes from a previous longer payload
        int remaining = Capacity - 4 - buffer.Length;
        if (remaining > 0)
        {
            byte[] zeros = new byte[Math.Min(remaining, 64)];
            accessor.WriteArray(4 + buffer.Length, zeros, 0, zeros.Length);
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// A single running instance entry in the shared memory registry.
/// </summary>
internal sealed class InstanceEntry
{
    /// <summary>Unique window identifier (e.g. "cascade-hello-cascade-1").</summary>
    public required string WindowId { get; init; }

    /// <summary>TCP loopback port the instance's MCP server is listening on.</summary>
    public required int Port { get; init; }

    /// <summary>Window title for display.</summary>
    public required string Title { get; init; }

    /// <summary>Process ID for stale-entry pruning.</summary>
    public required int Pid { get; init; }

    /// <summary>Whether this window currently has OS focus.</summary>
    public bool Focused { get; set; }

    /// <summary>Timestamp of last activation (for multi-window tie-breaking).</summary>
    public long ActivatedAt { get; set; }
}

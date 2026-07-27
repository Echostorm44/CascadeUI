namespace Cascade.UI;

/// <summary>
/// In-memory HTTP data cache with LRU eviction, stale-while-revalidate support,
/// and concurrent request deduplication. Shared across all components.
/// </summary>
internal sealed class FetchCacheEngine
{
    private readonly Lock syncLock = new();
    private readonly Dictionary<string, CacheEntry> entries = new();
    private readonly LinkedList<string> lruOrder = new();
    private readonly Dictionary<string, object> inFlightRequests = new();
    private readonly int maxEntries;

    internal static FetchCacheEngine Instance { get; } = new();

    internal FetchCacheEngine(int maxEntries = 1000)
    {
        this.maxEntries = maxEntries;
    }

    /// <summary>
    /// Attempts to retrieve a cached value. Returns null if the key is not cached.
    /// The result includes a staleness indicator for stale-while-revalidate patterns.
    /// </summary>
    internal CacheResult<T>? TryGet<T>(string key)
    {
        lock (syncLock)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            TouchLru(entry);

            var isStale = entry.Duration.HasValue &&
                DateTime.UtcNow - entry.Timestamp > entry.Duration.Value;

            return new CacheResult<T>((T)entry.Value!, isStale);
        }
    }

    /// <summary>
    /// Stores a value in the cache with an optional duration.
    /// Evicts the least-recently-used entry if the cache is full.
    /// </summary>
    internal void Set<T>(string key, T value, TimeSpan? duration)
    {
        lock (syncLock)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                lruOrder.Remove(existing.LruNode);
                existing.Value = value;
                existing.Timestamp = DateTime.UtcNow;
                existing.Duration = duration;
                lruOrder.AddLast(existing.LruNode);
            }
            else
            {
                var node = lruOrder.AddLast(key);
                entries[key] = new CacheEntry
                {
                    Value = value,
                    Timestamp = DateTime.UtcNow,
                    Duration = duration,
                    LruNode = node
                };
                Evict();
            }
        }
    }

    /// <summary>
    /// Gets a cached value or fetches it. Deduplicates concurrent requests for the
    /// same key — only one fetch runs, and all callers share the result.
    /// </summary>
    internal async Task<T> GetOrFetchAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> fetcher,
        TimeSpan? duration,
        CancellationToken ct)
    {
        var cached = TryGet<T>(key);
        if (cached.HasValue && !cached.Value.IsStale)
        {
            return cached.Value.Value;
        }

        Task<T> inFlight;
        lock (syncLock)
        {
            if (inFlightRequests.TryGetValue(key, out var existing))
            {
                inFlight = (Task<T>)existing;
            }
            else
            {
                var task = FetchAndCacheAsync(key, fetcher, duration, ct);
                inFlightRequests[key] = task;
                inFlight = task;
            }
        }

        return await inFlight.ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidates a specific cache key.
    /// </summary>
    internal void Invalidate(string key)
    {
        lock (syncLock)
        {
            RemoveEntry(key);
        }
    }

    /// <summary>
    /// Invalidates all cache keys matching the given prefix.
    /// </summary>
    internal void InvalidatePrefix(string prefix)
    {
        lock (syncLock)
        {
            var keysToRemove = entries.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                RemoveEntry(key);
            }
        }
    }

    /// <summary>
    /// Clears the entire cache and cancels all in-flight tracking.
    /// </summary>
    internal void Clear()
    {
        lock (syncLock)
        {
            entries.Clear();
            lruOrder.Clear();
            inFlightRequests.Clear();
        }
    }

    private async Task<T> FetchAndCacheAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> fetcher,
        TimeSpan? duration,
        CancellationToken ct)
    {
        try
        {
            var result = await fetcher(ct).ConfigureAwait(false);
            Set(key, result, duration);
            return result;
        }
        finally
        {
            lock (syncLock)
            {
                inFlightRequests.Remove(key);
            }
        }
    }

    private void RemoveEntry(string key)
    {
        if (entries.TryGetValue(key, out var entry))
        {
            lruOrder.Remove(entry.LruNode);
            entries.Remove(key);
        }
    }

    private void TouchLru(CacheEntry entry)
    {
        lruOrder.Remove(entry.LruNode);
        lruOrder.AddLast(entry.LruNode);
    }

    private void Evict()
    {
        while (entries.Count > maxEntries && lruOrder.First is not null)
        {
            var oldest = lruOrder.First.Value;
            lruOrder.RemoveFirst();
            entries.Remove(oldest);
        }
    }

    private sealed class CacheEntry
    {
        internal object? Value { get; set; }
        internal DateTime Timestamp { get; set; }
        internal TimeSpan? Duration { get; set; }
        internal LinkedListNode<string> LruNode { get; set; } = null!;
    }
}

/// <summary>
/// Result from the fetch cache, including whether the entry is stale.
/// </summary>
internal readonly record struct CacheResult<T>(T Value, bool IsStale);

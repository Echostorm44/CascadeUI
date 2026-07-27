namespace Cascade.UI;

/// <summary>
/// Thread-safe LRU cache for shaped text results. Avoids reshaping the same
/// text at the same font/size combination. Uses a lock-protected dictionary
/// with a linked list for eviction ordering.
/// </summary>
internal sealed class GlyphCache
{
    readonly int maxEntries;
    readonly Dictionary<GlyphCacheKey, LinkedListNode<GlyphCacheEntry>> map;
    readonly LinkedList<GlyphCacheEntry> order;
    readonly Lock lockObj = new();

    internal GlyphCache(int maxEntries)
    {
        this.maxEntries = maxEntries;
        map = new Dictionary<GlyphCacheKey, LinkedListNode<GlyphCacheEntry>>(maxEntries);
        order = new LinkedList<GlyphCacheEntry>();
    }

    /// <summary>
    /// Attempts to retrieve a cached shaping result for the given key.
    /// Moves the entry to the front of the LRU list on hit.
    /// </summary>
    internal bool TryGet(GlyphCacheKey key, out ShaperResult result)
    {
        lock (lockObj)
        {
            if (map.TryGetValue(key, out var node))
            {
                order.Remove(node);
                order.AddFirst(node);
                result = node.Value.Result;
                return true;
            }
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Stores a shaping result in the cache. Evicts the least-recently-used
    /// entry when the cache exceeds its maximum size.
    /// </summary>
    internal void Add(GlyphCacheKey key, ShaperResult result)
    {
        lock (lockObj)
        {
            // If key already exists, remove the old entry
            if (map.TryGetValue(key, out var existing))
            {
                order.Remove(existing);
                map.Remove(key);
            }

            // Evict oldest entries if at capacity
            while (map.Count >= maxEntries && order.Last != null)
            {
                var evicted = order.Last!;
                order.RemoveLast();
                map.Remove(evicted.Value.Key);
            }

            // Insert at front (most recently used)
            var entry = new GlyphCacheEntry(key, result);
            var node = new LinkedListNode<GlyphCacheEntry>(entry);
            order.AddFirst(node);
            map[key] = node;
        }
    }

    /// <summary>
    /// Returns the number of entries currently in the cache.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (lockObj)
            {
                return map.Count;
            }
        }
    }

    /// <summary>
    /// Removes all entries from the cache.
    /// </summary>
    internal void Clear()
    {
        lock (lockObj)
        {
            map.Clear();
            order.Clear();
        }
    }
}

/// <summary>
/// Cache key for shaped text results: (text content, font path, font size).
/// </summary>
internal readonly record struct GlyphCacheKey(
    string Text,
    string FontPath,
    float FontSize
);

/// <summary>
/// An entry in the glyph cache combining key and result for LRU tracking.
/// </summary>
internal readonly record struct GlyphCacheEntry(
    GlyphCacheKey Key,
    ShaperResult Result
);

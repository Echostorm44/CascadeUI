namespace Cascade.UI;

/// <summary>
/// Caches <see cref="TextLayoutResult"/> instances keyed on (text, options) so
/// that repeated layouts of identical text with identical options — which is
/// the norm for static Label controls — skip the full shaping + line-breaking
/// pipeline entirely.
/// </summary>
/// <remarks>
/// The cache is single-threaded; all text layout happens on the UI thread
/// (layout pass + paint pass). If cross-thread layout is added in the future,
/// this needs a lock or per-thread instance.
///
/// Eviction policy: simple cap + FIFO trim. Text rarely churns, so a plain
/// dictionary with a size cap is sufficient — we don't need full LRU
/// bookkeeping and its per-access allocations.
/// </remarks>
internal static class TextLayoutCache
{
    private const int MaxEntries = 2048;
    private const int TrimTo = 1536;

    private static readonly Dictionary<TextLayoutCacheKey, TextLayoutResult> cache = new(capacity: 512);

    // Insertion-order queue for simple FIFO eviction. Allocates a small amount
    // on growth (one-time) and trims itself in place.
    private static readonly Queue<TextLayoutCacheKey> insertionOrder = new(capacity: 512);

    internal static int Count => cache.Count;

    internal static bool TryGet(string text, in TextLayoutOptions options, out TextLayoutResult result)
    {
        var key = new TextLayoutCacheKey(text, options);
        return cache.TryGetValue(key, out result!);
    }

    internal static void Add(string text, in TextLayoutOptions options, TextLayoutResult result)
    {
        var key = new TextLayoutCacheKey(text, options);
        if (cache.ContainsKey(key))
        {
            return;
        }
        cache[key] = result;
        insertionOrder.Enqueue(key);

        if (cache.Count > MaxEntries)
        {
            while (cache.Count > TrimTo && insertionOrder.Count > 0)
            {
                var oldest = insertionOrder.Dequeue();
                cache.Remove(oldest);
            }
        }
    }

    internal static void Clear()
    {
        cache.Clear();
        insertionOrder.Clear();
    }
}

/// <summary>
/// Composite key for the text layout cache. Value type so lookups allocate
/// nothing. Text is held by reference; equality uses ordinal string compare
/// with a fast reference-equal shortcut (common case: the same string literal
/// or interned localization string appears across frames).
/// </summary>
internal readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
{
    private readonly string text;
    private readonly TextLayoutOptions options;
    private readonly int hash;

    public TextLayoutCacheKey(string text, in TextLayoutOptions options)
    {
        this.text = text;
        this.options = options;
        // Precompute hash so dictionary lookups don't recompute on every call.
        var hc = new HashCode();
        hc.Add(text, StringComparer.Ordinal);
        hc.Add(options);
        this.hash = hc.ToHashCode();
    }

    public bool Equals(TextLayoutCacheKey other)
    {
        if (hash != other.hash)
        {
            return false;
        }
        if (!ReferenceEquals(text, other.text))
        {
            if (!string.Equals(text, other.text, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return options.Equals(other.options);
    }

    public override bool Equals(object? obj)
    {
        return obj is TextLayoutCacheKey other && Equals(other);
    }

    public override int GetHashCode() => hash;
}

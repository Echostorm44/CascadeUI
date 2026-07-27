namespace Cascade.UI;

/// <summary>
/// In-memory cache for data fetched via <c>FetchAsync</c>. Cached results
/// are shared across components and do not survive app restart.
/// </summary>
public static class FetchCache
{
    /// <summary>
    /// Invalidates a specific cache key. The next <c>FetchAsync</c> call
    /// with this key will re-execute the fetcher.
    /// </summary>
    public static void Invalidate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        FetchCacheEngine.Instance.Invalidate(key);
    }

    /// <summary>
    /// Invalidates all keys matching a prefix when <paramref name="prefixMatch"/>
    /// is <c>true</c>, or the exact key when <c>false</c>.
    /// </summary>
    public static void Invalidate(string keyPrefix, bool prefixMatch)
    {
        ArgumentNullException.ThrowIfNull(keyPrefix);

        if (prefixMatch)
        {
            FetchCacheEngine.Instance.InvalidatePrefix(keyPrefix);
        }
        else
        {
            FetchCacheEngine.Instance.Invalidate(keyPrefix);
        }
    }

    /// <summary>
    /// Clears the entire fetch cache.
    /// </summary>
    public static void Clear()
    {
        FetchCacheEngine.Instance.Clear();
    }
}

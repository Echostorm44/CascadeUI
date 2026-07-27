namespace Cascade.UI;

/// <summary>
/// Options for controlling async data fetching behavior via
/// <c>FetchAsync</c> on <see cref="Component"/>.
/// </summary>
public record FetchOptions
{
    /// <summary>
    /// How long to cache the result before considering it stale.
    /// Default: <c>null</c> — re-fetches on every mount.
    /// </summary>
    public TimeSpan? CacheDuration { get; init; }

    /// <summary>
    /// Key for cache deduplication. Required if <see cref="CacheDuration"/> is set.
    /// Multiple components fetching the same key share the cached result.
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// If true, show previous data while refreshing (Refreshing state).
    /// If false, show Loading state on refresh. Default: <c>true</c>.
    /// </summary>
    public bool KeepPreviousData { get; init; } = true;
}

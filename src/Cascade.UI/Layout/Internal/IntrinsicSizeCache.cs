using System.Runtime.CompilerServices;

namespace Cascade.UI;

/// <summary>
/// Caches intrinsic size computations to prevent repeated measurement of
/// the same subtree. Keyed by node identity and constraint axis value.
/// Invalidated when the dirty tracker marks a node dirty.
/// </summary>
internal sealed class IntrinsicSizeCache
{
    private readonly Dictionary<CacheKey, float> cache = new();

    /// <summary>
    /// Clears all cached intrinsic sizes. Called at the start of a layout
    /// pass or when the tree structure changes.
    /// </summary>
    internal void Clear()
    {
        cache.Clear();
    }

    /// <summary>
    /// Tries to retrieve a cached intrinsic size for the given node and query.
    /// </summary>
    internal bool TryGet(Node node, IntrinsicQuery query, float constraintValue, out float result)
    {
        var key = new CacheKey(
            RuntimeHelpers.GetHashCode(node), query, constraintValue);
        return cache.TryGetValue(key, out result);
    }

    /// <summary>
    /// Stores a computed intrinsic size in the cache.
    /// </summary>
    internal void Store(Node node, IntrinsicQuery query, float constraintValue, float result)
    {
        var key = new CacheKey(
            RuntimeHelpers.GetHashCode(node), query, constraintValue);
        cache[key] = result;
    }

    /// <summary>
    /// Removes cached entries for a specific node. Called when the dirty
    /// tracker marks the node dirty.
    /// </summary>
    internal void Invalidate(Node node)
    {
        int nodeId = RuntimeHelpers.GetHashCode(node);
        var keysToRemove = new List<CacheKey>();

        foreach (var key in cache.Keys)
        {
            if (key.NodeId == nodeId)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            cache.Remove(key);
        }
    }

    private readonly record struct CacheKey(
        int NodeId,
        IntrinsicQuery Query,
        float ConstraintValue);
}

/// <summary>
/// The type of intrinsic size query being performed.
/// </summary>
internal enum IntrinsicQuery
{
    MinWidth,
    MaxWidth,
    MinHeight,
    MaxHeight
}

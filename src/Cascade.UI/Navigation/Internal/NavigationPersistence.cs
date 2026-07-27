using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cascade.UI;

/// <summary>
/// Persists navigation stack state to storage, enabling the app to restore
/// the user's location after restart. Only persists route paths and
/// parameters — component instances are recreated via <see cref="RouteResolver"/>.
/// </summary>
internal sealed class NavigationPersistence
{
    private const string NavStateKey = "__cascade_nav_state";
    private readonly IStorageEngine storageEngine;
    private readonly RouteResolver routeResolver;

    public NavigationPersistence(IStorageEngine storageEngine, RouteResolver routeResolver)
    {
        this.storageEngine = storageEngine;
        this.routeResolver = routeResolver;
    }

    /// <summary>
    /// Saves the current navigation stack as a list of route paths with parameters.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Serializes simple DTO types (PersistedNavEntry) that are always safe.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Serializes simple DTO types (PersistedNavEntry) that are always safe.")]
    public void Save(IReadOnlyList<PersistedNavEntry> stack)
    {
        var json = JsonSerializer.Serialize(stack);
        storageEngine.Write(NavStateKey, json);
    }

    /// <summary>
    /// Restores the navigation stack from persisted state. Returns an empty
    /// list if no state is saved, deserialization fails, or routes have changed.
    /// </summary>
    [RequiresUnreferencedCode("Route resolution uses reflection to discover Component types with [Route] attributes.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Deserializes simple DTO types (PersistedNavEntry) that are always safe.")]
    public List<PersistedNavEntry> Restore()
    {
        var json = storageEngine.Read(NavStateKey);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        List<PersistedNavEntry>? persisted;
        try
        {
            persisted = JsonSerializer.Deserialize<List<PersistedNavEntry>>(json);
        }
        catch (JsonException)
        {
            return [];
        }

        if (persisted == null || persisted.Count == 0)
        {
            return [];
        }

        var result = new List<PersistedNavEntry>();
        foreach (var entry in persisted)
        {
            var match = routeResolver.Resolve(entry.Path);
            if (match != null)
            {
                // Merge persisted params that are not already in route params
                foreach (var kvp in entry.Parameters)
                {
                    match.Parameters.TryAdd(kvp.Key, kvp.Value);
                }

                result.Add(new PersistedNavEntry
                {
                    Path = entry.Path,
                    Parameters = new Dictionary<string, string>(match.Parameters, StringComparer.OrdinalIgnoreCase),
                });
            }
        }

        return result;
    }

    /// <summary>Clears persisted navigation state.</summary>
    public void Clear()
    {
        storageEngine.Remove(NavStateKey);
    }
}

/// <summary>
/// Represents a single persisted entry in the navigation history.
/// Contains only the path and parameters — component instances are
/// recreated on restore via route resolution.
/// </summary>
internal sealed class PersistedNavEntry
{
    public string Path { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = [];
}

namespace Cascade.UI.Core.Internal;

/// <summary>
/// Captures and restores component state across hot reload cycles.
/// Preserves reactive field values so the user doesn't lose form input,
/// scroll position, or other transient state during development.
/// </summary>
/// <remarks>
/// State preserved:
/// - Reactive field values (signals)
/// - Form input values
/// - Scroll positions
/// - Dialog open/closed state
/// - Navigation stack position
///
/// State NOT preserved (reset on reload):
/// - In-flight async operations
/// - Timer callbacks
/// - Event handler registrations
/// </remarks>
internal sealed class StatePreserver
{
    private readonly Dictionary<string, Dictionary<string, object?>> snapshots = [];

    /// <summary>Number of component states currently stored.</summary>
    public int StoredStateCount => snapshots.Count;

    /// <summary>
    /// Captures the current state of all mounted components as a snapshot.
    /// </summary>
    public StateSnapshot CaptureSnapshot()
    {
        var entries = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var kvp in snapshots)
        {
            entries[kvp.Key] = new Dictionary<string, object?>(kvp.Value);
        }
        return new StateSnapshot(entries);
    }

    /// <summary>
    /// Restores component state from a previously captured snapshot.
    /// </summary>
    public void RestoreSnapshot(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshots.Clear();
        foreach (var kvp in snapshot.Entries)
        {
            snapshots[kvp.Key] = new Dictionary<string, object?>(kvp.Value);
        }
    }

    /// <summary>
    /// Stores a named value for a component identified by its key.
    /// </summary>
    public void StoreValue(string componentKey, string fieldName, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentKey);
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        if (!snapshots.TryGetValue(componentKey, out var fields))
        {
            fields = [];
            snapshots[componentKey] = fields;
        }
        fields[fieldName] = value;
    }

    /// <summary>
    /// Retrieves a stored value for a component field.
    /// Returns the value and true if found, default and false if not.
    /// </summary>
    public bool TryGetValue(string componentKey, string fieldName, out object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentKey);
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        if (snapshots.TryGetValue(componentKey, out var fields) &&
            fields.TryGetValue(fieldName, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Clears all stored state.</summary>
    public void Clear()
    {
        snapshots.Clear();
    }

    /// <summary>
    /// Checks whether state exists for a given component.
    /// </summary>
    public bool HasState(string componentKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentKey);
        return snapshots.ContainsKey(componentKey);
    }

    /// <summary>
    /// Gets all field names stored for a component.
    /// </summary>
    public IReadOnlyCollection<string> GetStoredFields(string componentKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentKey);
        if (snapshots.TryGetValue(componentKey, out var fields))
        {
            return fields.Keys;
        }
        return [];
    }
}

/// <summary>
/// An immutable snapshot of component state at a point in time.
/// </summary>
internal sealed class StateSnapshot
{
    public StateSnapshot(Dictionary<string, Dictionary<string, object?>> entries)
    {
        Entries = entries;
    }

    /// <summary>Component key → (field name → value) mappings.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, object?>> Entries { get; }

    /// <summary>Number of components in this snapshot.</summary>
    public int ComponentCount => Entries.Count;
}

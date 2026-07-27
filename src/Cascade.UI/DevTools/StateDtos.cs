using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// A computed (derived) property with its dependency chain.
/// </summary>
/// <remarks>
/// Extracted from <see cref="StatePanel"/> so it remains visible to
/// <see cref="NodeTreeWalker"/> and the MCP tool surface in Release builds
/// compiled with <c>CASCADE_DEVTOOLS</c>.
/// </remarks>
public sealed class ComputedEntry
{
    /// <summary>Owning component type name.</summary>
    public required string ComponentName { get; init; }

    /// <summary>Property name.</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the value.</summary>
    public required string ValueType { get; init; }

    /// <summary>Current cached value serialized to string.</summary>
    public required string CurrentValue { get; init; }

    /// <summary>Signals this computed reads from.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>Whether the cached value is stale and will recompute on next access.</summary>
    public bool IsStale { get; init; }
}

/// <summary>
/// An async data loading state.
/// </summary>
public sealed class AsyncDataEntry
{
    /// <summary>Owning component type name.</summary>
    public required string ComponentName { get; init; }

    /// <summary>Field name.</summary>
    public required string FieldName { get; init; }

    /// <summary>CLR type of the loaded value.</summary>
    public required string ValueType { get; init; }

    /// <summary>Current state: Loading, Loaded, Error, Idle.</summary>
    public required string State { get; init; }

    /// <summary>Current value if loaded.</summary>
    public string? CurrentValue { get; init; }

    /// <summary>Error message if in error state.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// A local storage entry (key-value pair persisted across sessions).
/// </summary>
public sealed class LocalStorageEntry
{
    /// <summary>Storage key.</summary>
    public required string Key { get; init; }

    /// <summary>Stored value serialized to string.</summary>
    public required string Value { get; init; }

    /// <summary>Value type name.</summary>
    public required string ValueType { get; init; }

    /// <summary>Size in bytes of the serialized value.</summary>
    public int SizeBytes { get; init; }
}

/// <summary>
/// Undo stack entry representing a state mutation.
/// </summary>
public sealed class UndoEntry
{
    /// <summary>Description of the state change.</summary>
    public required string Description { get; init; }

    /// <summary>Component that initiated the change.</summary>
    public required string ComponentName { get; init; }

    /// <summary>Signal that was modified.</summary>
    public required string SignalName { get; init; }

    /// <summary>Previous value.</summary>
    public required string OldValue { get; init; }

    /// <summary>New value.</summary>
    public required string NewValue { get; init; }

    /// <summary>Timestamp of the change.</summary>
    public DateTime Timestamp { get; init; }
}

#endif

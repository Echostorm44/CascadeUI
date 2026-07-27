namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// A reactive signal field with its metadata. Extracted from
/// <c>StatePanel</c> so the DTO is available in Release agent builds.
/// </summary>
public sealed class SignalEntry
{
    /// <summary>Owning component type name.</summary>
    public required string ComponentName { get; init; }

    /// <summary>Field name.</summary>
    public required string FieldName { get; init; }

    /// <summary>CLR type of the value.</summary>
    public required string ValueType { get; init; }

    /// <summary>Current value serialized to string.</summary>
    public required string CurrentValue { get; init; }

    /// <summary>Whether the field is readonly (opted out of reactivity).</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Number of components subscribing to this signal.</summary>
    public int SubscriberCount { get; init; }

    /// <summary>Number of times this signal has been written.</summary>
    public int WriteCount { get; init; }
}

#endif

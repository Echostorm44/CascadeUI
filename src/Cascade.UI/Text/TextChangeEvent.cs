namespace Cascade.UI;

/// <summary>
/// Describes a change that occurred in a <see cref="TextDocument"/>.
/// </summary>
public readonly record struct TextChangeEvent(
    /// <summary>Where the change occurred (character offset).</summary>
    int Offset,

    /// <summary>Number of characters removed.</summary>
    int OldLength,

    /// <summary>Number of characters inserted.</summary>
    int NewLength,

    /// <summary>The text that was inserted.</summary>
    ReadOnlyMemory<char> InsertedText
);

namespace Cascade.UI;

/// <summary>
/// Result of a hit test against laid-out text, mapping a point to a document offset.
/// </summary>
public readonly record struct TextHitResult(
    /// <summary>Character offset in the document.</summary>
    int Offset,

    /// <summary>True if the hit is on the trailing edge of the character (after it), false if on the leading edge (before it).</summary>
    bool IsTrailingEdge,

    /// <summary>False if the click was beyond the end of the line.</summary>
    bool IsInsideText
);

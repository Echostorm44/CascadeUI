namespace Cascade.UI;

/// <summary>
/// Rendering information for the text caret, computed from the current
/// selection and text layout.
/// </summary>
public readonly record struct CaretInfo(
    float X,
    float Y,
    float Height,
    /// <summary>Which way the caret leans at a bidirectional boundary.</summary>
    TextDirection Direction
);

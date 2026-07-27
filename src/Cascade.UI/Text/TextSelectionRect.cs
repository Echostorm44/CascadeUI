namespace Cascade.UI;

/// <summary>
/// A rectangle representing one line's portion of a text selection.
/// Multi-line or bidirectional selections produce multiple rects.
/// </summary>
public readonly record struct TextSelectionRect(
    float X,
    float Y,
    float Width,
    float Height,
    int LineIndex
);

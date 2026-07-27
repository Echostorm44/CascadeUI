namespace Cascade.UI;

/// <summary>
/// A selection range within a <see cref="TextDocument"/>, defined by an
/// anchor (where selection started) and a focus (where the caret is).
/// </summary>
public readonly record struct TextSelection(
    TextPosition Anchor,
    TextPosition Focus
)
{
    /// <summary>True when anchor and focus are at the same offset (caret with no highlighted range).</summary>
    public bool IsCollapsed => Anchor.Offset == Focus.Offset;

    /// <summary>Start of the normalized range (always &lt;= End).</summary>
    public int Start => Math.Min(Anchor.Offset, Focus.Offset);

    /// <summary>End of the normalized range (always &gt;= Start).</summary>
    public int End => Math.Max(Anchor.Offset, Focus.Offset);

    /// <summary>Number of characters in the selection.</summary>
    public int Length => End - Start;

    /// <summary>True when the focus is at or after the anchor.</summary>
    public bool IsForward => Focus.Offset >= Anchor.Offset;

    /// <summary>Creates a collapsed selection (caret) at the given offset.</summary>
    public static TextSelection Collapsed(int offset)
    {
        return new(new TextPosition(offset), new TextPosition(offset));
    }

    /// <summary>Creates a collapsed selection (caret) at the given position.</summary>
    public static TextSelection Collapsed(TextPosition position)
    {
        return new(position, position);
    }

    /// <summary>Creates a selection spanning the entire document.</summary>
    public static TextSelection All(TextDocument document)
    {
        return new(TextPosition.Zero, new TextPosition(document.Length));
    }
}

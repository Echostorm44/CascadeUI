namespace Cascade.UI;

/// <summary>
/// Static methods for word boundary detection using the Unicode Text
/// Segmentation algorithm (UAX #29).
/// </summary>
public static class TextBoundary
{
    /// <summary>Returns the offset of the word start before the given position.</summary>
    public static int PreviousWordBoundary(TextDocument document, int offset)
    {
        return WordBoundary.FindWordStart(document, offset);
    }

    /// <summary>Returns the offset of the word end after the given position.</summary>
    public static int NextWordBoundary(TextDocument document, int offset)
    {
        return WordBoundary.FindWordEnd(document, offset);
    }

    /// <summary>Returns the word range (start, end) at the given position.</summary>
    public static (int Start, int End) WordAt(TextDocument document, int offset)
    {
        return WordBoundary.WordAt(document, offset);
    }
}

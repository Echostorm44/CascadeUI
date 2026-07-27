namespace Cascade.UI;

/// <summary>
/// Represents the current state of an IME composition session, including
/// the provisional text, cursor position, and underline segments.
/// </summary>
public readonly record struct TextComposition(
    /// <summary>
    /// The provisional text being composed (e.g., Pinyin romanization,
    /// partial Hangul syllable).
    /// </summary>
    string Text,

    /// <summary>
    /// Cursor position within the composition text. For Japanese input,
    /// this may be within the text as the user navigates clauses.
    /// </summary>
    int CursorOffset,

    /// <summary>
    /// Underline segments provided by the OS, indicating which parts
    /// of the composition are targeted vs non-targeted.
    /// </summary>
    IReadOnlyList<CompositionSegment> Segments
);

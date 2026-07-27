namespace Cascade.UI;

/// <summary>
/// Describes a caret movement direction and granularity. Used with
/// <see cref="TextEditingEngine.MoveCaret"/> to navigate within text.
/// </summary>
public enum CaretMovement
{
    /// <summary>Left arrow (visual direction).</summary>
    Left,

    /// <summary>Right arrow (visual direction).</summary>
    Right,

    /// <summary>Logical: toward document start.</summary>
    PreviousCharacter,

    /// <summary>Logical: toward document end.</summary>
    NextCharacter,

    /// <summary>Ctrl+Left / Option+Left.</summary>
    PreviousWord,

    /// <summary>Ctrl+Right / Option+Right.</summary>
    NextWord,

    /// <summary>Home key.</summary>
    LineStart,

    /// <summary>End key.</summary>
    LineEnd,

    /// <summary>Up arrow.</summary>
    PreviousLine,

    /// <summary>Down arrow.</summary>
    NextLine,

    /// <summary>Ctrl+Up (Windows) / Option+Up (macOS).</summary>
    PreviousParagraph,

    /// <summary>Ctrl+Down / Option+Down.</summary>
    NextParagraph,

    /// <summary>Ctrl+Home / Cmd+Up.</summary>
    DocumentStart,

    /// <summary>Ctrl+End / Cmd+Down.</summary>
    DocumentEnd,

    /// <summary>Page Up (for TextArea with scroll).</summary>
    PageUp,

    /// <summary>Page Down (for TextArea with scroll).</summary>
    PageDown
}

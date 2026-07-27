namespace Cascade.UI;

/// <summary>
/// Accessibility interface for text editing controls. Provides text content,
/// cursor position, and navigation information for screen readers.
/// </summary>
public interface ITextAccessibility
{
    /// <summary>Returns the full text content for the accessible value.</summary>
    string GetAccessibleText();

    /// <summary>Returns the cursor position as (line, column) for announcement.</summary>
    (int Line, int Column) GetCursorLineColumn();

    /// <summary>Returns the character at the current cursor for character-by-character reading.</summary>
    string GetCharacterAtCursor();

    /// <summary>Returns the word at the current cursor for word-by-word reading.</summary>
    string GetWordAtCursor();

    /// <summary>Returns the current line text for line-by-line reading.</summary>
    string GetLineAtCursor();
}

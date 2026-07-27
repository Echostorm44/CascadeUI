namespace Cascade.UI;

/// <summary>
/// Configuration options for a <see cref="TextEditingEngine"/> instance.
/// </summary>
public record TextEditingOptions
{
    /// <summary>Whether the engine supports multiple lines.</summary>
    public bool Multiline { get; init; } = false;

    /// <summary>Whether text wraps at the available width (only relevant when Multiline is true).</summary>
    public bool WordWrap { get; init; } = true;

    /// <summary>Whether the Tab key inserts a tab or indent (multiline mode).</summary>
    public bool AcceptTab { get; init; } = false;

    /// <summary>Number of spaces per tab stop.</summary>
    public int TabWidth { get; init; } = 4;

    /// <summary>When true, Tab inserts spaces instead of a tab character.</summary>
    public bool InsertSpacesForTab { get; init; } = true;

    /// <summary>Maximum character count (0 = unlimited).</summary>
    public int MaxLength { get; init; } = 0;

    /// <summary>When true, typing overwrites the character at the caret instead of inserting.</summary>
    public bool OverwriteMode { get; init; } = false;

    /// <summary>When true, bracket and quote auto-pairing is enabled (for code editor scenarios).</summary>
    public bool AutoPairBrackets { get; init; } = false;
}

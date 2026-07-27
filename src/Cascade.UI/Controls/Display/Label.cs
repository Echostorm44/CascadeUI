namespace Cascade.UI;

/// <summary>
/// Text label with support for wrapping, truncation, styling, and selection.
/// The fundamental text display control — used directly or as the base for
/// higher-level text display.
/// </summary>
public sealed class Label : Node
{
    /// <summary>
    /// Creates a label with a string.
    /// </summary>
    /// <param name="text">The text to display.</param>
    public Label(string text)
    {
        Text = text;
        LocText = default;
    }

    /// <summary>
    /// Creates a label with a localization key.
    /// </summary>
    /// <param name="text">The localization key.</param>
    public Label(LocKey text)
    {
        Text = null;
        LocText = text;
    }

    /// <summary>The text to display (null when using a LocKey).</summary>
    public string? Text { get; }

    /// <summary>The localization key (default when using a string).</summary>
    public LocKey LocText { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal TextStyle? TextStyleOverride { get; set; }
    internal FontFamily? FontFamilyOverride { get; set; }
    internal ColorValue? TextColorOverride { get; set; }
    internal TextOverflow OverflowMode { get; set; } = TextOverflow.Clip;
    internal int? MaxLineCount { get; set; }
    internal TextWrap WrapMode { get; set; } = TextWrap.NoWrap;
    internal TextAlignment Alignment { get; set; } = TextAlignment.Start;
    internal bool IsSelectable { get; set; }
    internal TextDecoration DecorationMode { get; set; } = TextDecoration.None;

    // ── Styling ───────────────────────────────────────────────────────

    /// <summary>Sets the text style (size, weight, line height).</summary>
    public Label Style(TextStyle style)
    {
        TextStyleOverride = style;
        return this;
    }

    /// <summary>Sets the font size, keeping weight and line height at defaults.</summary>
    public Label FontSize(float size)
    {
        TextStyleOverride = new TextStyle(size, FontWeight.Regular, size * 1.4f);
        return this;
    }

    /// <summary>Makes the text bold, preserving the current size if set.</summary>
    public Label Bold()
    {
        var current = TextStyleOverride ?? new TextStyle(14f, FontWeight.Regular, 14f * 1.4f);
        TextStyleOverride = new TextStyle(current.Size, FontWeight.Bold, current.LineHeight);
        return this;
    }

    /// <summary>Sets the font family for this label.</summary>
    public Label FontFamily(FontFamily family)
    {
        FontFamilyOverride = family;
        return this;
    }

    /// <summary>Sets the text color.</summary>
    public Label Color(ColorValue color)
    {
        TextColorOverride = color;
        return this;
    }

    // ── Wrapping and truncation ───────────────────────────────────────

    /// <summary>Sets the text overflow behavior.</summary>
    public Label Overflow(TextOverflow overflow)
    {
        OverflowMode = overflow;
        return this;
    }

    /// <summary>Sets the maximum number of lines before truncation.</summary>
    public Label MaxLines(int maxLines)
    {
        MaxLineCount = maxLines;
        return this;
    }

    /// <summary>Sets the text wrapping behavior.</summary>
    public Label Wrap(TextWrap wrap)
    {
        WrapMode = wrap;
        return this;
    }

    // ── Alignment ─────────────────────────────────────────────────────

    /// <summary>Sets the text alignment within the label bounds.</summary>
    public Label TextAlign(TextAlignment alignment)
    {
        Alignment = alignment;
        return this;
    }

    // ── Selection ─────────────────────────────────────────────────────

    /// <summary>Enables or disables text selection.</summary>
    public Label Selectable(bool enabled)
    {
        IsSelectable = enabled;
        return this;
    }

    // ── Decoration ────────────────────────────────────────────────────

    /// <summary>Sets text decoration (underline, strikethrough).</summary>
    public Label Decoration(TextDecoration decoration)
    {
        DecorationMode = decoration;
        return this;
    }
}

/// <summary>
/// Text overflow behavior when text exceeds its container.
/// </summary>
public enum TextOverflow
{
    /// <summary>Text is clipped at the boundary.</summary>
    Clip,

    /// <summary>Text is truncated with an ellipsis ("…").</summary>
    Ellipsis,

    /// <summary>Text fades out at the boundary.</summary>
    Fade
}

/// <summary>
/// Text wrapping behavior.
/// </summary>
public enum TextWrap
{
    /// <summary>No wrapping — text stays on one line.</summary>
    NoWrap,

    /// <summary>Wraps at any character when needed.</summary>
    Wrap,

    /// <summary>Wraps at word boundaries.</summary>
    WordWrap
}

/// <summary>
/// Text alignment within a label.
/// </summary>
public enum TextAlignment
{
    /// <summary>Left-aligned (or start-aligned in RTL).</summary>
    Start,

    /// <summary>Center-aligned.</summary>
    Center,

    /// <summary>Right-aligned (or end-aligned in RTL).</summary>
    End,

    /// <summary>Justified text — stretched to fill the line.</summary>
    Justify
}

/// <summary>
/// Text decoration applied to a label.
/// </summary>
public enum TextDecoration
{
    /// <summary>No decoration.</summary>
    None,

    /// <summary>Underline.</summary>
    Underline,

    /// <summary>Line through the middle of text.</summary>
    Strikethrough
}

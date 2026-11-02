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

    internal float? FontSizeOverride { get; set; }
    internal FontWeight? FontWeightOverride { get; set; }

    /// <summary>Line height as a multiple of the font size; 0 for the font's natural line height.</summary>
    internal float? LineHeightOverride { get; set; }
    internal bool IsItalic { get; set; }

    /// <summary>
    /// The size, weight and line height set on this label, the unset ones filled from the body
    /// style; null when none is set. Setting it sets all three.
    /// </summary>
    internal TextStyle? TextStyleOverride
    {
        get => FontSizeOverride is null && FontWeightOverride is null && LineHeightOverride is null
            ? null
            : new TextStyle(FontSizeOverride ?? LayoutSolver.BodyFontSize, FontWeightOverride ?? UI.FontWeight.Regular, LineHeightOverride ?? 0f);
        set
        {
            FontSizeOverride = value?.Size;
            FontWeightOverride = value?.Weight;
            LineHeightOverride = value?.LineHeight;
        }
    }

    /// <summary>The style the label is measured and drawn in: its own settings over <paramref name="body"/>'s size and weight.</summary>
    internal TextStyle ResolveStyle(TextStyle body)
        => new(FontSizeOverride ?? body.Size, FontWeightOverride ?? body.Weight, LineHeightOverride ?? 0f);

    /// <summary>The line height in pixels for <paramref name="style"/>, or 0 for the font's natural one.</summary>
    internal static float LineHeightPixels(TextStyle style)
        => style.LineHeight > 0f && float.IsFinite(style.LineHeight) ? style.Size * style.LineHeight : 0f;

    internal FontFamily? FontFamilyOverride { get; set; }
    internal ColorValue? TextColorOverride { get; set; }
    internal TextOverflow OverflowMode { get; set; } = TextOverflow.Clip;
    internal int? MaxLineCount { get; set; }
    internal TextWrap WrapMode { get; set; } = TextWrap.WordWrap;
    internal TextAlignment Alignment { get; set; } = TextAlignment.Start;
    internal bool IsSelectable { get; set; }
    internal TextDecoration DecorationMode { get; set; } = TextDecoration.None;
    internal TextOutline? OutlineStyle { get; set; }

    // ── Styling ───────────────────────────────────────────────────────

    /// <summary>
    /// Sets the text style: size, weight and line height (a multiple of the size; 0 for the font's
    /// natural line height).
    /// </summary>
    public Label Style(TextStyle style)
    {
        TextStyleOverride = style;
        return this;
    }

    /// <summary>Sets the font size in logical pixels, keeping any weight and line height already set.</summary>
    public Label FontSize(float size)
    {
        FontSizeOverride = size;
        return this;
    }

    /// <summary>Sets the font weight (Thin 100 to Black 900). The bundled Inter has every weight.</summary>
    public Label FontWeight(FontWeight weight)
    {
        FontWeightOverride = weight;
        return this;
    }

    /// <summary>Makes the text bold (weight 700), keeping the size.</summary>
    public Label Bold()
    {
        FontWeightOverride = UI.FontWeight.Bold;
        return this;
    }

    /// <summary>
    /// Sets the italic style. A family without an italic font (no <c>-Italic</c> sibling file) is
    /// drawn upright.
    /// </summary>
    public Label Italic(bool italic = true)
    {
        IsItalic = italic;
        return this;
    }

    /// <summary>
    /// Sets the line height as a multiple of the font size (CSS <c>line-height</c>: 1.5 is 24 px
    /// lines for 16 px text); the extra space is split above and below each line. 0 restores the
    /// font's natural line height.
    /// </summary>
    public Label LineHeight(float multiple)
    {
        LineHeightOverride = multiple > 0f && float.IsFinite(multiple) ? multiple : 0f;
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

    /// <summary>
    /// Outlines the text: each glyph grown outward by <paramref name="width"/> logical pixels with
    /// round corners, in <paramref name="color"/>, drawn under the text. Keeps light text readable
    /// over a busy image. The outline does not change the label's size; it may draw up to
    /// <paramref name="width"/> past its edges. A width of 0 (or less) removes it.
    /// </summary>
    public Label Outline(ColorValue color, float width = 2f)
    {
        OutlineStyle = width > 0f ? new TextOutline(color, width) : null;
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

    /// <summary>
    /// Sets the text wrapping behavior (default: <see cref="TextWrap.WordWrap"/>). Use
    /// <see cref="TextWrap.NoWrap"/> with <see cref="TextOverflow.Ellipsis"/> for a one-line label cut at
    /// a character ("https://github.com/Echo…"), not at the last word that fits.
    /// </summary>
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

    /// <summary>
    /// Lets the user select the text with the mouse (drag; double-click for a word, triple-click
    /// for all of it) and copy it with Ctrl+C.
    /// </summary>
    public Label Selectable(bool enabled = true)
    {
        IsSelectable = enabled;
        AnySelectable |= enabled;
        return this;
    }

    /// <summary>Whether any label was ever made selectable: pointer hit-testing for selection is skipped until one is.</summary>
    internal static bool AnySelectable { get; private set; }

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
    /// <summary>No wrapping: each line (split only at hard line breaks) stays whole; wider text overflows or is ellipsized.</summary>
    NoWrap,

    /// <summary>Wraps when needed, at Unicode line-break opportunities (currently the same as <see cref="WordWrap"/>).</summary>
    Wrap,

    /// <summary>Wraps at word boundaries (Unicode line-break opportunities, UAX #14). The default.</summary>
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
/// An outline around text, drawn under it: each glyph grown outward by <see cref="Width"/>
/// logical pixels with round corners, in <see cref="Color"/>. Widths are rendered to a quarter of
/// a device pixel, up to about 63 device pixels.
/// </summary>
/// <param name="Color">The outline colour.</param>
/// <param name="Width">How far the outline reaches past each glyph's edge, in logical pixels.</param>
public readonly record struct TextOutline(ColorValue Color, float Width)
{
    /// <summary>Whether the outline draws anything: a positive width and a visible colour.</summary>
    public bool IsVisible => Width > 0f && Color.A > 0f;
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

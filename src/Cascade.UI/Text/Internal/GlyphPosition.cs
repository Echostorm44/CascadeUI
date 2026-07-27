namespace Cascade.UI;

/// <summary>
/// A positioned glyph within a laid-out text line, produced by the text shaping pipeline.
/// </summary>
public readonly record struct GlyphPosition(
    /// <summary>Font-specific glyph identifier.</summary>
    uint GlyphId,
    /// <summary>Horizontal position relative to the line origin.</summary>
    float X,
    /// <summary>Vertical position relative to the line origin.</summary>
    float Y,
    /// <summary>Horizontal advance to the next glyph.</summary>
    float AdvanceWidth,
    /// <summary>Index into the original text that this glyph maps to (cluster index).</summary>
    int ClusterIndex
)
{
    /// <summary>
    /// WP-3531: true if this glyph belongs to a right-to-left bidi run, i.e. its
    /// logical-leading edge is on the right. Defaults to false (LTR), so the
    /// pure-LTR fast path and all existing constructions are unaffected; the bidi
    /// layout path sets it for RTL-run glyphs so caret geometry/movement can be
    /// visual-order-correct.
    /// </summary>
    public bool Rtl { get; init; }
}

namespace Cascade.UI;

/// <summary>
/// The result of laying out text through the text layout engine (HarfBuzz shaping,
/// line breaking, glyph positioning). Used by <see cref="TextEditingEngine"/> and
/// <see cref="TextHitTest"/> for caret positioning and selection geometry.
/// </summary>
public sealed class TextLayoutResult
{
    // Underlying arrays are stored directly so hot-path callers inside the
    // framework can iterate without allocating an IEnumerable enumerator.
    // The public IReadOnlyList properties are views over the same arrays.
    private readonly LayoutLine[] lines;

    /// <summary>The original text that was laid out.</summary>
    public string Text { get; }

    /// <summary>The laid-out lines, each containing positioned glyphs.</summary>
    public IReadOnlyList<LayoutLine> Lines => lines;

    /// <summary>
    /// Direct array accessor for hot-path internal code. Zero-alloc foreach.
    /// </summary>
    internal LayoutLine[] LinesArray => lines;

    /// <summary>Total bounding box enclosing all laid-out text.</summary>
    public Size BoundingBox { get; }

    /// <summary>
    /// Total advance box including trailing whitespace.
    /// Used for caret positioning where the cursor should advance past trailing spaces.
    /// </summary>
    public Size AdvanceBox { get; }

    internal TextLayoutResult(string text, LayoutLine[] lines, Size boundingBox, Size advanceBox)
    {
        Text = text;
        this.lines = lines;
        BoundingBox = boundingBox;
        AdvanceBox = advanceBox;
    }

    /// <summary>
    /// Performs a hit test, mapping a point in layout coordinates to a document offset.
    /// </summary>
    public TextHitResult HitTest(float x, float y)
    {
        if (Lines.Count == 0)
        {
            return new TextHitResult(0, false, false);
        }

        // Find the line that contains the Y coordinate
        LayoutLine line = Lines[^1];
        for (int i = 0; i < Lines.Count; i++)
        {
            if (y < Lines[i].Y + Lines[i].Height || i == Lines.Count - 1)
            {
                line = Lines[i];
                break;
            }
        }

        if (line.Glyphs.Count == 0)
        {
            return new TextHitResult(line.TextStart, false, y >= line.Y && y < line.Y + line.Height);
        }

        float localX = x - line.X;

        // Before the first glyph
        if (localX <= 0)
        {
            return new TextHitResult(line.TextStart, false, false);
        }

        // After the last glyph
        var lastGlyph = line.Glyphs[^1];
        if (localX >= lastGlyph.X + lastGlyph.AdvanceWidth)
        {
            return new TextHitResult(line.TextStart + line.TextLength, false, false);
        }

        // Search within glyphs
        for (int i = 0; i < line.Glyphs.Count; i++)
        {
            var glyph = line.Glyphs[i];
            float glyphEnd = glyph.X + glyph.AdvanceWidth;

            if (localX >= glyph.X && localX < glyphEnd)
            {
                float mid = glyph.X + glyph.AdvanceWidth / 2;
                bool isTrailing = localX >= mid;
                return new TextHitResult(glyph.ClusterIndex, isTrailing, true);
            }
        }

        return new TextHitResult(line.TextStart + line.TextLength, false, false);
    }

    /// <summary>
    /// Computes the caret rendering information for the given document offset and affinity.
    /// </summary>
    public CaretInfo GetCaretInfo(int offset, TextAffinity affinity = TextAffinity.Downstream)
    {
        if (Lines.Count == 0)
        {
            return new CaretInfo(0, 0, 0, TextDirection.LeftToRight);
        }

        // Find the line containing the offset
        LayoutLine line = Lines[0];
        for (int i = 0; i < Lines.Count; i++)
        {
            int lineEnd = Lines[i].TextStart + Lines[i].TextLength;
            if (offset >= Lines[i].TextStart && offset <= lineEnd)
            {
                line = Lines[i];
                // At a line boundary with upstream affinity, prefer previous line
                if (offset == Lines[i].TextStart && affinity == TextAffinity.Upstream && i > 0)
                {
                    line = Lines[i - 1];
                }
                break;
            }
        }

        // WP-3531: visual-order-correct caret X + direction (RTL/mixed text). For
        // pure-LTR lines every glyph has Rtl=false, so this reduces to the former
        // left-edge scan and LTR-only callers are unaffected.
        float caretX = line.X + VisualCaret.XForOffset(line, offset);
        TextDirection direction = VisualCaret.DirectionAt(line, offset);

        return new CaretInfo(caretX, line.Y, line.Height, direction);
    }

    /// <summary>
    /// Returns the line index containing the given character offset.
    /// </summary>
    public int GetLineIndexForOffset(int offset)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            int lineEnd = Lines[i].TextStart + Lines[i].TextLength;
            if (offset >= Lines[i].TextStart && offset <= lineEnd)
            {
                return i;
            }
        }
        return Lines.Count > 0 ? Lines.Count - 1 : 0;
    }
}

/// <summary>
/// A single line within a <see cref="TextLayoutResult"/>, containing its text range,
/// position, dimensions, baseline, and individual glyph positions.
/// </summary>
public sealed class LayoutLine
{
    /// <summary>Character offset in the original text where this line starts.</summary>
    public int TextStart { get; }

    /// <summary>Number of characters in this line.</summary>
    public int TextLength { get; }

    /// <summary>Horizontal position of the line origin (affected by alignment).</summary>
    public float X { get; }

    /// <summary>Vertical position of the top of the line.</summary>
    public float Y { get; }

    /// <summary>Width of the visible text on this line (excluding trailing whitespace).</summary>
    public float Width { get; }

    /// <summary>
    /// Full advance width of the line including trailing whitespace.
    /// Used for caret positioning where the caret should advance past trailing spaces.
    /// </summary>
    public float AdvanceWidth { get; }

    /// <summary>Total line height (ascent + descent + leading × multiplier).</summary>
    public float Height { get; }

    /// <summary>Distance from the top of the line to the text baseline.</summary>
    public float Baseline { get; }

    // Underlying array stored directly for zero-alloc hot-path iteration.
    private readonly GlyphPosition[] glyphs;
    private readonly FontRun[]? fontRuns;

    /// <summary>Individual glyph positions on this line, in visual order.</summary>
    public IReadOnlyList<GlyphPosition> Glyphs => glyphs;

    /// <summary>
    /// Direct array accessor for hot-path internal code. Zero-alloc foreach.
    /// </summary>
    internal GlyphPosition[] GlyphsArray => glyphs;

    /// <summary>
    /// Font runs for this line. Null when all glyphs use the primary font (common case).
    /// When non-null, each run specifies a contiguous range of glyphs sharing a font.
    /// </summary>
    internal IReadOnlyList<FontRun>? FontRuns => fontRuns;

    /// <summary>Direct array accessor for font runs. Null when single-font.</summary>
    internal FontRun[]? FontRunsArray => fontRuns;

    internal LayoutLine(
        int textStart, int textLength,
        float x, float y,
        float width, float advanceWidth, float height, float baseline,
        GlyphPosition[] glyphs,
        FontRun[]? fontRuns = null)
    {
        TextStart = textStart;
        TextLength = textLength;
        X = x;
        Y = y;
        Width = width;
        AdvanceWidth = advanceWidth;
        Height = height;
        Baseline = baseline;
        this.glyphs = glyphs;
        this.fontRuns = fontRuns;
    }
}

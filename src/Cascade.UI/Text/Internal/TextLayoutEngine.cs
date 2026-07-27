using System.Collections.Concurrent;

namespace Cascade.UI;

// TextAlignment and TextOverflow enums are defined in Controls/Display/Label.cs

/// <summary>
/// Options controlling text layout: font, size, wrapping, alignment, overflow.
/// </summary>
/// <remarks>
/// A readonly struct so that allocating options per layout call is free.
/// Supports object-initializer syntax via <c>required init</c> properties,
/// so existing call sites continue to work unchanged.
/// </remarks>
public readonly struct TextLayoutOptions : IEquatable<TextLayoutOptions>
{
    /// <summary>Path to the font file (TTF/OTF/TTC).</summary>
    public required string FontPath { get; init; }

    /// <summary>Font size in logical pixels.</summary>
    public float FontSize { get; init; }

    /// <summary>Maximum available width before wrapping. Infinity = no wrap.</summary>
    public float MaxWidth { get; init; }

    /// <summary>Maximum available height. Lines beyond this are truncated.</summary>
    public float MaxHeight { get; init; }

    /// <summary>Maximum number of lines (0 = unlimited).</summary>
    public int MaxLines { get; init; }

    /// <summary>Horizontal alignment within the layout width.</summary>
    public TextAlignment Alignment { get; init; }

    /// <summary>How to handle text overflow.</summary>
    public TextOverflow Overflow { get; init; }

    /// <summary>Line height as a multiplier of the font's natural line height.</summary>
    public float LineHeightMultiplier { get; init; }

    public TextLayoutOptions()
    {
        FontPath = null!; // required; initializer must set.
        FontSize = 14f;
        MaxWidth = float.PositiveInfinity;
        MaxHeight = float.PositiveInfinity;
        MaxLines = 0;
        Alignment = TextAlignment.Start;
        Overflow = TextOverflow.Clip;
        LineHeightMultiplier = 1.0f;
    }

    public bool Equals(TextLayoutOptions other)
    {
        return FontSize == other.FontSize
            && MaxWidth.Equals(other.MaxWidth)
            && MaxHeight.Equals(other.MaxHeight)
            && MaxLines == other.MaxLines
            && Alignment == other.Alignment
            && Overflow == other.Overflow
            && LineHeightMultiplier == other.LineHeightMultiplier
            && string.Equals(FontPath, other.FontPath, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is TextLayoutOptions other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(FontPath, StringComparer.Ordinal);
        hc.Add(FontSize);
        hc.Add(MaxWidth);
        hc.Add(MaxHeight);
        hc.Add(MaxLines);
        hc.Add((int)Alignment);
        hc.Add((int)Overflow);
        hc.Add(LineHeightMultiplier);
        return hc.ToHashCode();
    }

    public static bool operator ==(TextLayoutOptions left, TextLayoutOptions right) => left.Equals(right);
    public static bool operator !=(TextLayoutOptions left, TextLayoutOptions right) => !left.Equals(right);
}

/// <summary>
/// Main text layout pipeline. Takes text + font + constraints and produces
/// a <see cref="TextLayoutResult"/> with positioned lines and glyphs.
/// Coordinates shaping (HarfBuzz), line breaking (UAX #14), and alignment.
/// </summary>
public static class TextLayoutEngine
{
    static readonly GlyphCache glyphCache = new(maxEntries: 4096);
    static readonly ConcurrentDictionary<string, HarfBuzzShaper> shaperCache = new();

    // Cache for GetGlyphVisualBounds. Keyed on (text, fontPath, fontSize).
    // Values never change at runtime for a given key — glyph metrics are
    // immutable per font file — so the cache is unbounded-safe for typical
    // apps where emoji/icon sets are bounded (~100-1000 entries total).
    // Called 80× per EmojiPicker paint (once per emoji cell); each uncached
    // call allocates a HarfBuzz Buffer + shapes the glyph.
    static readonly ConcurrentDictionary<GlyphVisualBoundsKey, GlyphVisualBounds?> glyphVisualBoundsCache = new();

    // Cache for GetGlyphInkBounds (FreeType-rasterized ink box). Same immutable
    // keying as glyphVisualBoundsCache; used for pixel-perfect centering.
    static readonly ConcurrentDictionary<GlyphVisualBoundsKey, GlyphVisualBounds?> glyphInkBoundsCache = new();

    readonly struct GlyphVisualBoundsKey : IEquatable<GlyphVisualBoundsKey>
    {
        readonly string text;
        readonly string fontPath;
        readonly float fontSize;
        readonly int hash;

        public GlyphVisualBoundsKey(string text, string fontPath, float fontSize)
        {
            this.text = text;
            this.fontPath = fontPath;
            this.fontSize = fontSize;
            this.hash = HashCode.Combine(
                text.GetHashCode(StringComparison.Ordinal),
                fontPath.GetHashCode(StringComparison.Ordinal),
                fontSize);
        }

        public bool Equals(GlyphVisualBoundsKey other)
        {
            if (hash != other.hash)
            {
                return false;
            }
            return fontSize == other.fontSize
                && string.Equals(fontPath, other.fontPath, StringComparison.Ordinal)
                && string.Equals(text, other.text, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is GlyphVisualBoundsKey k && Equals(k);
        }

        public override int GetHashCode() => hash;
    }

    /// <summary>
    /// Lays out text with the given options, producing positioned lines and glyphs.
    /// </summary>
    /// <remarks>
    /// Results are cached by (text, options). Repeated calls for identical
    /// inputs — the common case for static labels across frames — return the
    /// same cached <see cref="TextLayoutResult"/> without re-running shaping or
    /// line breaking.
    /// </remarks>
    public static TextLayoutResult Layout(string text, TextLayoutOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextLayoutResult(text ?? "", [], Size.Zero, Size.Zero);
        }

        if (TextLayoutCache.TryGet(text, options, out var cached))
        {
            return cached;
        }

        var result = LayoutUncached(text, options);
        TextLayoutCache.Add(text, options, result);
        return result;
    }

    private static TextLayoutResult LayoutUncached(string text, TextLayoutOptions options)
    {
        var shaper = GetOrCreateShaper(options.FontPath);
        var metrics = shaper.GetMetrics(options.FontSize);

        float ascent = Math.Abs(metrics.Ascender);
        float descent = Math.Abs(metrics.Descender);
        float naturalLineHeight = ascent + descent + Math.Abs(metrics.LineGap);
        float lineHeight = naturalLineHeight * Math.Max(options.LineHeightMultiplier, 0.1f);

        // WP-3521b: mixed-direction (bidi) text needs per-run shaping + visual
        // reordering. Pure-LTR and pure-RTL (a single directional run) stay on the
        // fast path below, unchanged. The cheap MayHaveBidi scan skips the bidi
        // analysis entirely for the overwhelmingly common Latin-only case.
        // WP-3521b/3531: mixed-direction (>1 run) text goes to the bidi layout path.
        // A single RTL run stays on the fast path (HarfBuzz already shapes it in
        // visual order, and the fast path keeps its line-breaking/ellipsis), but we
        // remember it is RTL so the glyphs can be flagged for visual caret movement.
        bool fastPathRtl = false;
        if (MayHaveBidi(text))
        {
            var bidi = Etch.Text.Unicode.Minimal.BidiAlgorithm.Analyze(text.AsSpan());
            if (bidi.Runs.Length > 1)
            {
                return LayoutBidi(text, options, ascent, lineHeight);
            }
            fastPathRtl = bidi.Runs.Length == 1 && (bidi.Runs[0].Level & 1) == 1;
        }

        // Shape the full text
        var shaped = ShapeWithCache(shaper, text, options.FontSize);

        // Find line break opportunities
        var breaks = LineBreaker.FindBreakOpportunities(text.AsSpan());

        // Build cumulative advance map: xAtOffset[i] = total advance up to text offset i
        float[] xAtOffset = BuildXAtOffsetMap(text, shaped.Glyphs, shaped.TotalAdvance);

        // Break into lines
        var lines = BreakIntoLines(
            text, shaped.Glyphs, breaks, xAtOffset,
            options.MaxWidth, lineHeight, ascent, shaped.TotalAdvance);

        // Truncate to max lines
        if (options.MaxLines > 0 && lines.Count > options.MaxLines)
        {
            if (options.Overflow == TextOverflow.Ellipsis && lines.Count > 0)
            {
                TruncateWithEllipsis(lines, options, shaper, xAtOffset, lineHeight, ascent);
            }
            while (lines.Count > options.MaxLines)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        // Single-line width overflow: truncate with ellipsis even when line count is within limit
        if (options.Overflow == TextOverflow.Ellipsis && lines.Count > 0 && options.MaxLines > 0)
        {
            int lastKeptIndex = Math.Min(lines.Count, options.MaxLines) - 1;
            if (!float.IsPositiveInfinity(options.MaxWidth) && lines[lastKeptIndex].Width > options.MaxWidth)
            {
                TruncateWithEllipsis(lines, options, shaper, xAtOffset, lineHeight, ascent);
            }
        }

        // Truncate lines that exceed max height
        if (!float.IsPositiveInfinity(options.MaxHeight))
        {
            while (lines.Count > 0 && lines[^1].Y + lines[^1].Height > options.MaxHeight)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        // Apply horizontal alignment. WP-3532: a pure-RTL fast-path paragraph
        // auto-right-aligns under Start/default alignment, matching the bidi path.
        float layoutWidth = float.IsPositiveInfinity(options.MaxWidth) ? 0 : options.MaxWidth;
        TextAlignment effectiveAlignment = fastPathRtl && options.Alignment == TextAlignment.Start
            ? TextAlignment.End
            : options.Alignment;
        ApplyAlignment(lines, effectiveAlignment, layoutWidth);

        // Per-character font fallback: replace .notdef glyphs (GlyphId == 0)
        // with glyphs shaped using a fallback font that covers the codepoint.
        ApplyFontFallback(lines, text, options.FontPath, options.FontSize);

        // WP-3531: flag a pure-RTL fast-path line's glyphs as RTL so the visual
        // caret model treats their logical-leading edge as the right edge.
        if (fastPathRtl)
        {
            MarkGlyphsRtl(lines);
        }

        // Compute bounding box (visual width, excludes trailing whitespace)
        // and advance box (advance width, includes trailing whitespace for caret positioning)
        float totalWidth = 0;
        float totalAdvanceWidth = 0;
        float totalHeight = 0;
        foreach (var line in lines)
        {
            float lineRight = line.X + line.Width;
            if (lineRight > totalWidth) { totalWidth = lineRight; }
            float lineAdvanceRight = line.X + line.AdvanceWidth;
            if (lineAdvanceRight > totalAdvanceWidth) { totalAdvanceWidth = lineAdvanceRight; }
            float lineBottom = line.Y + line.Height;
            if (lineBottom > totalHeight) { totalHeight = lineBottom; }
        }

        return new TextLayoutResult(text, lines.ToArray(),
            new Size(totalWidth, totalHeight),
            new Size(totalAdvanceWidth, totalHeight));
    }

    // ── Bidirectional layout (WP-3521b) ─────────────────────────────────

    /// <summary>
    /// Cheap pre-filter: returns true only if the text contains a character that
    /// could participate in bidi reordering (an RTL-script char or an explicit
    /// bidi control). Latin/CJK/emoji-only text returns false and skips the bidi
    /// analysis entirely, leaving the fast path untouched.
    /// </summary>
    static bool MayHaveBidi(string text)
    {
        foreach (char c in text)
        {
            int u = c;
            if ((u >= 0x0590 && u <= 0x08FF) || // Hebrew, Arabic, Syriac, Thaana, NKo, Samaritan, Mandaic, Arabic Ext-A
                (u >= 0xFB1D && u <= 0xFDFF) || // Hebrew + Arabic presentation forms-A
                (u >= 0xFE70 && u <= 0xFEFF) || // Arabic presentation forms-B
                u == 0x200E || u == 0x200F ||   // LRM / RLM
                (u >= 0x202A && u <= 0x202E) || // LRE/RLE/PDF/LRO/RLO
                (u >= 0x2066 && u <= 0x2069))   // isolates
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Lays out mixed-direction text. Each paragraph (split on '\n') is bidi-resolved,
    /// soft-wrapped to <see cref="TextLayoutOptions.MaxWidth"/> (WP-3532), and each
    /// resulting logical line range is reordered into visual order — sub-segmented by
    /// font and shaped with its own direction, then placed left-to-right. Base-RTL
    /// paragraphs auto-right-align under Start alignment. Bidi-aware ellipsis (visual-
    /// end truncation preserving per-font runs) is tracked separately as WP-3536.
    /// </summary>
    static TextLayoutResult LayoutBidi(string text, TextLayoutOptions options, float ascent, float lineHeight)
    {
        var lines = new List<LayoutLine>();
        var lineBaseRtl = new List<bool>();
        float y = 0;
        int n = text.Length;
        int segStart = 0;

        for (int i = 0; i <= n; i++)
        {
            bool atEnd = i == n;
            if (!atEnd && text[i] != '\n')
            {
                continue;
            }

            LayoutBidiParagraphInto(lines, lineBaseRtl, text, segStart, i, options, ascent, lineHeight, ref y);
            segStart = i + 1;

            if (atEnd)
            {
                break;
            }
        }

        // Truncate to max lines / max height (WP-3532), then WP-3536 bidi-aware
        // ellipsis on the last visible line when content was dropped or it overflows.
        bool ellipsis = options.Overflow == TextOverflow.Ellipsis;
        bool truncated = false;
        if (options.MaxLines > 0)
        {
            while (lines.Count > options.MaxLines)
            {
                lines.RemoveAt(lines.Count - 1);
                lineBaseRtl.RemoveAt(lineBaseRtl.Count - 1);
                truncated = true;
            }
        }
        if (!float.IsPositiveInfinity(options.MaxHeight))
        {
            while (lines.Count > 0 && lines[^1].Y + lines[^1].Height > options.MaxHeight)
            {
                lines.RemoveAt(lines.Count - 1);
                lineBaseRtl.RemoveAt(lineBaseRtl.Count - 1);
                truncated = true;
            }
        }

        if (ellipsis && lines.Count > 0)
        {
            bool lastOverflows = !float.IsPositiveInfinity(options.MaxWidth) && lines[^1].Width > options.MaxWidth;
            if (truncated || lastOverflows)
            {
                ApplyBidiEllipsis(lines, lineBaseRtl, lines.Count - 1, options);
            }
        }

        // WP-3532: base-RTL paragraphs auto-align to the right under Start/default
        // alignment (Start follows the paragraph direction); explicit Center/End/
        // Justify are honoured as-is for both directions.
        float layoutWidth = float.IsPositiveInfinity(options.MaxWidth) ? 0 : options.MaxWidth;
        ApplyBidiAlignment(lines, lineBaseRtl, options.Alignment, layoutWidth);

        float totalWidth = 0, totalAdvanceWidth = 0, totalHeight = 0;
        foreach (var line in lines)
        {
            float lineRight = line.X + line.Width;
            if (lineRight > totalWidth) { totalWidth = lineRight; }
            float lineAdvanceRight = line.X + line.AdvanceWidth;
            if (lineAdvanceRight > totalAdvanceWidth) { totalAdvanceWidth = lineAdvanceRight; }
            float lineBottom = line.Y + line.Height;
            if (lineBottom > totalHeight) { totalHeight = lineBottom; }
        }

        return new TextLayoutResult(text, lines.ToArray(),
            new Size(totalWidth, totalHeight),
            new Size(totalAdvanceWidth, totalHeight));
    }

    /// <summary>
    /// Lays out one paragraph (text[pStart..pEnd)) in visual order. Returns the
    /// line-relative glyphs, the total visual width, and the per-font runs.
    /// </summary>
    /// <summary>
    /// Lays out one paragraph (text[pStart..pEnd)) into one or more visual lines,
    /// appending each to <paramref name="lines"/>. The paragraph is bidi-resolved
    /// once, soft-wrapped to <see cref="TextLayoutOptions.MaxWidth"/> (WP-3532) at
    /// line-break opportunities, and each resulting logical line range is reordered
    /// into visual order and placed left-to-right.
    /// </summary>
    static void LayoutBidiParagraphInto(
        List<LayoutLine> lines, List<bool> lineBaseRtl,
        string text, int pStart, int pEnd, TextLayoutOptions options,
        float ascent, float lineHeight, ref float y)
    {
        int len = pEnd - pStart;
        if (len <= 0)
        {
            lines.Add(new LayoutLine(pStart, 0, 0, y, 0, 0, lineHeight, ascent, Array.Empty<GlyphPosition>(), null));
            lineBaseRtl.Add(false);
            y += lineHeight;
            return;
        }

        string para = text.Substring(pStart, len);
        var bidi = Etch.Text.Unicode.Minimal.BidiAlgorithm.Analyze(para.AsSpan());
        bool baseRtl = (bidi.ParagraphLevel & 1) == 1;
        var primaryShaper = GetOrCreateShaper(options.FontPath);

        foreach (var (rStart, rLen) in BreakBidiLines(para, bidi, options, primaryShaper))
        {
            var (glyphs, width, fontRuns) = PlaceBidiRange(text, para, pStart, rStart, rStart + rLen, bidi, options, primaryShaper);
            lines.Add(new LayoutLine(
                pStart + rStart, rLen, 0, y,
                width, width, lineHeight, ascent,
                glyphs, fontRuns.Length > 0 ? fontRuns : null));
            lineBaseRtl.Add(baseRtl);
            y += lineHeight;
        }
    }

    /// <summary>
    /// Greedily breaks a bidi paragraph into logical line ranges at line-break
    /// opportunities, measuring each character's advance via per-run directional
    /// shaping. Returns one range covering the whole paragraph when no width limit
    /// applies.
    /// </summary>
    static List<(int Start, int Len)> BreakBidiLines(
        string para, Etch.Text.Unicode.Minimal.BidiParagraphResult bidi,
        TextLayoutOptions options, HarfBuzzShaper primaryShaper)
    {
        int len = para.Length;
        float maxWidth = options.MaxWidth;
        if (float.IsPositiveInfinity(maxWidth) || maxWidth <= 0)
        {
            return new List<(int, int)> { (0, len) };
        }

        // Per-logical-character advance (summed over the cluster's glyphs).
        float[] adv = new float[len];
        foreach (var run in bidi.Runs)
        {
            bool rtl = (run.Level & 1) == 1;
            foreach ((string fontPath, int sStart, int sLen) in SegmentByFont(para, run.Start, run.Length, options.FontPath, primaryShaper))
            {
                var shaper = fontPath == options.FontPath ? primaryShaper : GetOrCreateShaper(fontPath);
                var shaped = shaper.ShapeDirectional(para.Substring(sStart, sLen), options.FontSize, rtl);
                foreach (var g in shaped.Glyphs)
                {
                    int ci = sStart + g.ClusterIndex;
                    if (ci >= 0 && ci < len)
                    {
                        adv[ci] += g.AdvanceWidth;
                    }
                }
            }
        }

        // A break may start a new line at any opportunity Position.
        var canBreakBefore = new bool[len + 1];
        foreach (var b in LineBreaker.FindBreakOpportunities(para.AsSpan()))
        {
            if (b.Position >= 0 && b.Position <= len)
            {
                canBreakBefore[b.Position] = true;
            }
        }

        var ranges = new List<(int, int)>();
        int lineStart = 0;
        while (lineStart < len)
        {
            float w = 0;
            int lastFit = -1;
            int i = lineStart;
            for (; i < len; i++)
            {
                w += adv[i];
                if (w > maxWidth && i > lineStart)
                {
                    break;
                }
                if (canBreakBefore[i + 1])
                {
                    lastFit = i + 1; // a break after char i fits within maxWidth
                }
            }

            int lineEnd = i >= len
                ? len                                                   // fits to paragraph end
                : (lastFit > lineStart ? lastFit : Math.Max(i, lineStart + 1)); // wrap, or force ≥1 char
            ranges.Add((lineStart, lineEnd - lineStart));
            lineStart = lineEnd;
        }

        if (ranges.Count == 0)
        {
            ranges.Add((0, len));
        }
        return ranges;
    }

    /// <summary>
    /// Reorders one logical line range [rStart, rEnd) of a bidi paragraph into
    /// visual order (clipping the paragraph's visual runs to the range) and places
    /// the glyphs left-to-right. Returns line-relative glyphs, visual width, and
    /// the per-font runs.
    /// </summary>
    static (GlyphPosition[] Glyphs, float Width, FontRun[] FontRuns) PlaceBidiRange(
        string text, string para, int pStart, int rStart, int rEnd,
        Etch.Text.Unicode.Minimal.BidiParagraphResult bidi, TextLayoutOptions options, HarfBuzzShaper primaryShaper)
    {
        var glyphs = new List<GlyphPosition>();
        var fontRuns = new List<FontRun>();
        float x = 0f;

        foreach (var run in bidi.Runs)
        {
            int cs = Math.Max(run.Start, rStart);
            int ce = Math.Min(run.Start + run.Length, rEnd);
            if (ce <= cs)
            {
                continue;
            }

            bool rtl = (run.Level & 1) == 1;
            var segs = SegmentByFont(para, cs, ce - cs, options.FontPath, primaryShaper);
            if (rtl)
            {
                segs.Reverse(); // visual order within an RTL run is reverse-logical
            }

            foreach ((string fontPath, int sStart, int sLen) in segs)
            {
                string segText = para.Substring(sStart, sLen);
                var shaper = fontPath == options.FontPath ? primaryShaper : GetOrCreateShaper(fontPath);
                var shaped = shaper.ShapeDirectional(segText, options.FontSize, rtl);

                int glyphStart = glyphs.Count;
                foreach (var g in shaped.Glyphs)
                {
                    int absLogical = pStart + sStart + g.ClusterIndex;
                    if (absLogical >= 0 && absLogical < text.Length && char.IsControl(text[absLogical]))
                    {
                        continue;
                    }
                    glyphs.Add(new GlyphPosition(g.GlyphId, x + g.X, g.Y, g.AdvanceWidth, absLogical) { Rtl = rtl });
                }
                x += shaped.TotalAdvance;
                if (glyphs.Count > glyphStart)
                {
                    fontRuns.Add(new FontRun(fontPath, glyphStart, glyphs.Count - glyphStart));
                }
            }
        }

        return (glyphs.ToArray(), x, fontRuns.ToArray());
    }

    /// <summary>
    /// Splits a character range into maximal same-font sub-segments (logical order),
    /// using the primary font where it covers the codepoint and font fallback
    /// otherwise — the per-run analogue of <see cref="ApplyFontFallback"/>.
    /// </summary>
    static List<(string FontPath, int Start, int Len)> SegmentByFont(
        string para, int start, int length, string primaryFontPath, HarfBuzzShaper primaryShaper)
    {
        var result = new List<(string, int, int)>();
        int end = start + length;
        string? curFont = null;
        int curStart = start;

        int i = start;
        while (i < end)
        {
            int cp;
            int adv;
            if (char.IsHighSurrogate(para[i]) && i + 1 < end && char.IsLowSurrogate(para[i + 1]))
            {
                cp = char.ConvertToUtf32(para[i], para[i + 1]);
                adv = 2;
            }
            else
            {
                cp = para[i];
                adv = 1;
            }

            string font = primaryFontPath;
            if (!primaryShaper.HasGlyph(cp))
            {
                string? fb = FontFallback.FindFallbackFont(cp);
                if (fb != null)
                {
                    font = fb;
                }
            }

            if (curFont == null)
            {
                curFont = font;
                curStart = i;
            }
            else if (font != curFont)
            {
                result.Add((curFont, curStart, i - curStart));
                curFont = font;
                curStart = i;
            }

            i += adv;
        }

        if (curFont != null && end > curStart)
        {
            result.Add((curFont, curStart, end - curStart));
        }

        return result;
    }

    // ── Shaper management ───────────────────────────────────────────────

    static HarfBuzzShaper GetOrCreateShaper(string fontPath)
    {
        return shaperCache.GetOrAdd(fontPath, static path => new HarfBuzzShaper(path));
    }

    /// <summary>
    /// Returns the visual bounding box of a glyph, including left side bearing.
    /// Uses font fallback to find the correct font for emoji/symbol glyphs.
    /// </summary>
    internal static GlyphVisualBounds? GetGlyphVisualBounds(string text, float fontSize, string fontPath)
    {
        // Fast path: cached result (the common case — emoji/icon strings
        // are a small bounded set repainted every frame).
        var key = new GlyphVisualBoundsKey(text, fontPath, fontSize);
        if (glyphVisualBoundsCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var shaper = GetOrCreateShaper(fontPath);

        // Check if the primary font has this glyph; if not, use the fallback font
        int codepoint = char.ConvertToUtf32(text, 0);
        if (!shaper.HasGlyph(codepoint))
        {
            string? fallbackPath = FontFallback.FindFallbackFont(codepoint);
            if (fallbackPath != null)
            {
                shaper = GetOrCreateShaper(fallbackPath);
            }
        }

        var result = shaper.GetGlyphVisualBounds(text, fontSize);
        glyphVisualBoundsCache[key] = result;
        return result;
    }

    /// <summary>
    /// Returns the FreeType-rasterized ink bounding box of a short text run, in
    /// the same shape and units as <see cref="GetGlyphVisualBounds"/>. Use this
    /// for pixel-perfect centering of single glyphs / initials, where the hinted
    /// bitmap is the ground truth and the outline extents lean slightly off.
    /// </summary>
    internal static GlyphVisualBounds? GetGlyphInkBounds(string text, float fontSize, string fontPath)
    {
        var key = new GlyphVisualBoundsKey(text, fontPath, fontSize);
        if (glyphInkBoundsCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var shaper = GetOrCreateShaper(fontPath);

        int codepoint = char.ConvertToUtf32(text, 0);
        if (!shaper.HasGlyph(codepoint))
        {
            string? fallbackPath = FontFallback.FindFallbackFont(codepoint);
            if (fallbackPath != null)
            {
                shaper = GetOrCreateShaper(fallbackPath);
            }
        }

        var result = shaper.GetGlyphInkBounds(text, fontSize);
        glyphInkBoundsCache[key] = result;
        return result;
    }

    static ShaperResult ShapeWithCache(HarfBuzzShaper shaper, string text, float fontSize)
    {
        var key = new GlyphCacheKey(text, shaper.FontPath, fontSize);
        if (glyphCache.TryGet(key, out var cached))
        {
            return cached;
        }

        var result = shaper.Shape(text, fontSize);
        glyphCache.Add(key, result);
        return result;
    }

    // ── Advance map ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a map from text offset to cumulative X advance.
    /// xAtOffset[i] = the X position at the start of the character at index i.
    /// xAtOffset[text.Length] = total advance.
    /// </summary>
    static float[] BuildXAtOffsetMap(string text, GlyphPosition[] glyphs, float totalAdvance)
    {
        var xAt = new float[text.Length + 1];
        int glyphIdx = 0;

        for (int offset = 0; offset <= text.Length; offset++)
        {
            // Skip glyphs whose cluster is before this offset
            while (glyphIdx < glyphs.Length && glyphs[glyphIdx].ClusterIndex < offset)
            {
                glyphIdx++;
            }

            if (glyphIdx < glyphs.Length && glyphs[glyphIdx].ClusterIndex == offset)
            {
                xAt[offset] = glyphs[glyphIdx].X;
            }
            else if (glyphIdx < glyphs.Length)
            {
                // Offset is inside a multi-char cluster — use the cluster start X
                xAt[offset] = glyphs[glyphIdx].X;
            }
            else
            {
                xAt[offset] = totalAdvance;
            }
        }

        // Ensure monotonically non-decreasing
        for (int i = 1; i <= text.Length; i++)
        {
            if (xAt[i] < xAt[i - 1])
            {
                xAt[i] = xAt[i - 1];
            }
        }

        return xAt;
    }

    // ── Line breaking ───────────────────────────────────────────────────

    static List<LayoutLine> BreakIntoLines(
        string text,
        GlyphPosition[] glyphs,
        List<LineBreakOpportunity> breaks,
        float[] xAtOffset,
        float maxWidth,
        float lineHeight,
        float baseline,
        float totalAdvance)
    {
        var lines = new List<LayoutLine>();

        // No wrapping: single line
        if (float.IsPositiveInfinity(maxWidth))
        {
            // Still split on mandatory breaks
            if (breaks.Exists(b => b.IsMandatory))
            {
                return BreakAtMandatoryOnly(text, glyphs, breaks, xAtOffset, lineHeight, baseline, totalAdvance);
            }
            lines.Add(CreateLine(text, glyphs, 0, text.Length, 0, xAtOffset, 0, lineHeight, baseline, totalAdvance));
            return lines;
        }

        int lineStart = 0;
        float lineStartX = 0;
        int lastOptionalBreak = -1;
        float currentY = 0;

        // Append a sentinel at end of text if no break there
        var allBreaks = new List<LineBreakOpportunity>(breaks);
        bool hasEndBreak = allBreaks.Count > 0 && allBreaks[^1].Position == text.Length;
        if (!hasEndBreak)
        {
            allBreaks.Add(new LineBreakOpportunity(text.Length, false));
        }

        foreach (var brk in allBreaks)
        {
            if (brk.IsMandatory)
            {
                // Emit line from lineStart to brk.Position
                lines.Add(CreateLine(text, glyphs, lineStart, brk.Position, lineStartX,
                    xAtOffset, currentY, lineHeight, baseline, totalAdvance));
                lineStart = brk.Position;
                lineStartX = xAtOffset[lineStart];
                currentY += lineHeight;
                lastOptionalBreak = -1;
                continue;
            }

            float widthToHere = xAtOffset[brk.Position] - lineStartX;

            if (widthToHere > maxWidth)
            {
                if (lastOptionalBreak > lineStart)
                {
                    // Break at the last opportunity that fit
                    lines.Add(CreateLine(text, glyphs, lineStart, lastOptionalBreak, lineStartX,
                        xAtOffset, currentY, lineHeight, baseline, totalAdvance));
                    lineStart = lastOptionalBreak;
                    lineStartX = xAtOffset[lineStart];
                    currentY += lineHeight;
                    lastOptionalBreak = -1;

                    // Re-check whether current break still overflows
                    widthToHere = xAtOffset[brk.Position] - lineStartX;
                    if (widthToHere > maxWidth)
                    {
                        // Force break at current position
                        lines.Add(CreateLine(text, glyphs, lineStart, brk.Position, lineStartX,
                            xAtOffset, currentY, lineHeight, baseline, totalAdvance));
                        lineStart = brk.Position;
                        lineStartX = xAtOffset[lineStart];
                        currentY += lineHeight;
                        continue;
                    }
                }
                else
                {
                    // No previous break opportunity — force break here
                    lines.Add(CreateLine(text, glyphs, lineStart, brk.Position, lineStartX,
                        xAtOffset, currentY, lineHeight, baseline, totalAdvance));
                    lineStart = brk.Position;
                    lineStartX = xAtOffset[lineStart];
                    currentY += lineHeight;
                    continue;
                }
            }

            lastOptionalBreak = brk.Position;
        }

        // Emit remaining text
        if (lineStart < text.Length)
        {
            lines.Add(CreateLine(text, glyphs, lineStart, text.Length, lineStartX,
                xAtOffset, currentY, lineHeight, baseline, totalAdvance));
        }

        return lines;
    }

    static List<LayoutLine> BreakAtMandatoryOnly(
        string text,
        GlyphPosition[] glyphs,
        List<LineBreakOpportunity> breaks,
        float[] xAtOffset,
        float lineHeight,
        float baseline,
        float totalAdvance)
    {
        var lines = new List<LayoutLine>();
        int lineStart = 0;
        float currentY = 0;

        foreach (var brk in breaks)
        {
            if (brk.IsMandatory)
            {
                lines.Add(CreateLine(text, glyphs, lineStart, brk.Position,
                    xAtOffset[lineStart], xAtOffset, currentY, lineHeight, baseline, totalAdvance));
                lineStart = brk.Position;
                currentY += lineHeight;
            }
        }

        if (lineStart < text.Length)
        {
            lines.Add(CreateLine(text, glyphs, lineStart, text.Length,
                xAtOffset[lineStart], xAtOffset, currentY, lineHeight, baseline, totalAdvance));
        }

        return lines;
    }

    static LayoutLine CreateLine(
        string text,
        GlyphPosition[] allGlyphs,
        int textStart,
        int textEnd,
        float lineStartX,
        float[] xAtOffset,
        float y,
        float lineHeight,
        float baseline,
        float totalAdvance)
    {
        // Gather glyphs belonging to this line, with X adjusted to line-relative coordinates.
        // Skip control characters (newlines, carriage returns) — they have no visual glyph
        // and would render as .notdef boxes.
        var lineGlyphs = new List<GlyphPosition>();
        foreach (var glyph in allGlyphs)
        {
            if (glyph.ClusterIndex >= textStart && glyph.ClusterIndex < textEnd)
            {
                char ch = text[glyph.ClusterIndex];
                if (char.IsControl(ch))
                {
                    continue;
                }

                lineGlyphs.Add(new GlyphPosition(
                    glyph.GlyphId,
                    glyph.X - lineStartX,
                    glyph.Y,
                    glyph.AdvanceWidth,
                    glyph.ClusterIndex
                ));
            }
        }

        float width = textEnd <= text.Length
            ? xAtOffset[textEnd] - lineStartX
            : totalAdvance - lineStartX;

        // Trim trailing whitespace width
        int trimEnd = textEnd;
        while (trimEnd > textStart && trimEnd <= text.Length && char.IsWhiteSpace(text[trimEnd - 1]))
        {
            trimEnd--;
        }
        float trimmedWidth = trimEnd > textStart ? xAtOffset[trimEnd] - lineStartX : 0;

        return new LayoutLine(
            textStart, textEnd - textStart,
            0, y,
            Math.Max(trimmedWidth, 0), Math.Max(width, 0), lineHeight, baseline,
            lineGlyphs.ToArray());
    }

    // ── Ellipsis truncation ─────────────────────────────────────────────

    static void TruncateWithEllipsis(
        List<LayoutLine> lines,
        TextLayoutOptions options,
        HarfBuzzShaper shaper,
        float[] xAtOffset,
        float lineHeight,
        float baseline)
    {
        int maxLines = options.MaxLines;
        if (maxLines <= 0)
        {
            maxLines = int.MaxValue;
        }

        // Determine which line needs truncation:
        // - Too many lines: truncate the last kept line
        // - Single-line (or last-line) width overflow: truncate that line
        int lineIndex;
        if (lines.Count > maxLines)
        {
            lineIndex = maxLines - 1;
        }
        else if (lines.Count > 0 && !float.IsPositiveInfinity(options.MaxWidth))
        {
            lineIndex = lines.Count - 1;
            if (lines[lineIndex].Width <= options.MaxWidth)
            {
                return;
            }
        }
        else
        {
            return;
        }

        var targetLine = lines[lineIndex];

        // Shape the ellipsis character
        var ellipsisShaped = shaper.Shape("\u2026", options.FontSize);
        float ellipsisWidth = ellipsisShaped.TotalAdvance;

        float availableWidth = float.IsPositiveInfinity(options.MaxWidth)
            ? float.MaxValue
            : options.MaxWidth;

        float targetWidth = availableWidth - ellipsisWidth;
        if (targetWidth < 0)
        {
            targetWidth = 0;
        }

        // Find how much of the target line's text fits
        int textStart = targetLine.TextStart;
        int textEnd = textStart + targetLine.TextLength;
        int truncateAt = textEnd;

        float lineStartX = textStart < xAtOffset.Length ? xAtOffset[textStart] : 0;

        for (int i = textStart; i < textEnd && i < xAtOffset.Length; i++)
        {
            if (xAtOffset[i] - lineStartX > targetWidth)
            {
                truncateAt = i;
                break;
            }
        }

        // Build truncated glyph list
        var truncatedGlyphs = new List<GlyphPosition>();
        foreach (var glyph in targetLine.Glyphs)
        {
            if (glyph.ClusterIndex < truncateAt)
            {
                truncatedGlyphs.Add(glyph);
            }
        }

        // Append ellipsis glyph(s)
        float ellipsisX = truncateAt > textStart && truncateAt < xAtOffset.Length
            ? xAtOffset[truncateAt] - lineStartX
            : targetLine.Width;

        foreach (var eg in ellipsisShaped.Glyphs)
        {
            truncatedGlyphs.Add(new GlyphPosition(
                eg.GlyphId,
                ellipsisX + eg.X,
                eg.Y,
                eg.AdvanceWidth,
                truncateAt
            ));
        }

        float newWidth = ellipsisX + ellipsisWidth;
        lines[lineIndex] = new LayoutLine(
            textStart, truncateAt - textStart,
            targetLine.X, targetLine.Y,
            newWidth, newWidth, lineHeight, baseline,
            truncatedGlyphs.ToArray());
    }

    // ── Alignment ───────────────────────────────────────────────────────

    /// <summary>
    /// WP-3531: rewrites every glyph on every line with <c>Rtl = true</c>. Used for
    /// pure-RTL text laid out on the fast path (HarfBuzz already produced visual
    /// order); the flag lets <see cref="VisualCaret"/> place the logical-leading
    /// caret edge on the right.
    /// </summary>
    static void MarkGlyphsRtl(List<LayoutLine> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var glyphs = (GlyphPosition[])line.Glyphs;
            bool changed = false;
            for (int g = 0; g < glyphs.Length; g++)
            {
                if (!glyphs[g].Rtl)
                {
                    glyphs[g] = glyphs[g] with { Rtl = true };
                    changed = true;
                }
            }
            if (changed)
            {
                lines[i] = new LayoutLine(
                    line.TextStart, line.TextLength,
                    line.X, line.Y,
                    line.Width, line.AdvanceWidth, line.Height, line.Baseline,
                    glyphs, line.FontRuns is { } runs ? (FontRun[])runs : null);
            }
        }
    }

    /// <summary>
    /// WP-3536: appends an ellipsis ("…") at the <em>visual</em> end of a truncated
    /// bidi line — the right edge for a base-LTR line, the left edge for base-RTL —
    /// trimming whole glyphs from that end so the line + ellipsis fits MaxWidth, while
    /// preserving the per-font runs of the surviving glyphs (a trimmed RTL line still
    /// carries fallback-font glyphs). Glyphs are stored in visual order (increasing X).
    /// </summary>
    static void ApplyBidiEllipsis(List<LayoutLine> lines, List<bool> lineBaseRtl, int index, TextLayoutOptions options)
    {
        var line = lines[index];
        bool rtl = index < lineBaseRtl.Count && lineBaseRtl[index];

        var shaper = GetOrCreateShaper(options.FontPath);
        var ell = shaper.ShapeDirectional("…", options.FontSize, false);
        float ellW = ell.TotalAdvance;
        float budget = float.IsPositiveInfinity(options.MaxWidth)
            ? float.PositiveInfinity
            : Math.Max(0f, options.MaxWidth - ellW);

        var glyphs = (GlyphPosition[])line.Glyphs;
        string[] glyphFont = BuildGlyphFontMap(line, options.FontPath);
        int ellLogical = line.TextStart + line.TextLength;

        var outGlyphs = new List<GlyphPosition>(glyphs.Length + 1);
        var outFonts = new List<string>(glyphs.Length + 1);
        float width;

        if (!rtl)
        {
            // Trim from the right (highest X), append the ellipsis at the right edge.
            int k = glyphs.Length;
            while (k > 0 && glyphs[k - 1].X + glyphs[k - 1].AdvanceWidth > budget)
            {
                k--;
            }
            float endX = k > 0 ? glyphs[k - 1].X + glyphs[k - 1].AdvanceWidth : 0f;
            for (int g = 0; g < k; g++)
            {
                outGlyphs.Add(glyphs[g]);
                outFonts.Add(glyphFont[g]);
            }
            foreach (var eg in ell.Glyphs)
            {
                outGlyphs.Add(new GlyphPosition(eg.GlyphId, endX + eg.X, eg.Y, eg.AdvanceWidth, ellLogical));
                outFonts.Add(options.FontPath);
            }
            width = endX + ellW;
        }
        else
        {
            // Base-RTL: the visual end is the left, so trim from the left (lowest X)
            // and place the ellipsis on the left, shifting the survivors right.
            float right = 0f;
            foreach (var g in glyphs)
            {
                right = Math.Max(right, g.X + g.AdvanceWidth);
            }
            int j = 0;
            while (j < glyphs.Length && right - glyphs[j].X > budget)
            {
                j++;
            }
            float newLeft = j < glyphs.Length ? glyphs[j].X : right;
            float shift = ellW - newLeft; // survivors start at ellW; ellipsis fills [0, ellW)
            foreach (var eg in ell.Glyphs)
            {
                outGlyphs.Add(new GlyphPosition(eg.GlyphId, eg.X, eg.Y, eg.AdvanceWidth, ellLogical));
                outFonts.Add(options.FontPath);
            }
            for (int g = j; g < glyphs.Length; g++)
            {
                outGlyphs.Add(glyphs[g] with { X = glyphs[g].X + shift });
                outFonts.Add(glyphFont[g]);
            }
            width = (right - newLeft) + ellW;
        }

        var fontRuns = GroupFontRuns(outFonts);
        lines[index] = new LayoutLine(
            line.TextStart, line.TextLength,
            line.X, line.Y,
            width, width, line.Height, line.Baseline,
            outGlyphs.ToArray(), fontRuns.Length > 0 ? fontRuns : null);
    }

    /// <summary>Per-glyph font path for a line, from its <see cref="LayoutLine.FontRuns"/>.</summary>
    static string[] BuildGlyphFontMap(LayoutLine line, string primaryFontPath)
    {
        var map = new string[line.Glyphs.Count];
        Array.Fill(map, primaryFontPath);
        if (line.FontRuns is { } runs)
        {
            foreach (var run in runs)
            {
                for (int g = run.GlyphStartIndex; g < run.GlyphStartIndex + run.GlyphCount && g < map.Length; g++)
                {
                    map[g] = run.FontPath;
                }
            }
        }
        return map;
    }

    /// <summary>Groups a per-glyph font list into maximal same-font <see cref="FontRun"/>s.</summary>
    static FontRun[] GroupFontRuns(List<string> fonts)
    {
        var runs = new List<FontRun>();
        int i = 0;
        while (i < fonts.Count)
        {
            int start = i;
            string f = fonts[i];
            while (i < fonts.Count && fonts[i] == f)
            {
                i++;
            }
            runs.Add(new FontRun(f, start, i - start));
        }
        return runs.ToArray();
    }

    /// <summary>
    /// WP-3532: per-line alignment for bidi text. <see cref="TextAlignment.Start"/>
    /// resolves to the right edge for base-RTL lines (so an RTL paragraph reads
    /// right-aligned by default), and to the left for LTR lines. Center/End are
    /// applied as written. Needs a finite layout width to anchor against.
    /// </summary>
    static void ApplyBidiAlignment(List<LayoutLine> lines, List<bool> baseRtl, TextAlignment alignment, float layoutWidth)
    {
        if (layoutWidth <= 0)
        {
            return;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            TextAlignment eff = alignment;
            if (eff == TextAlignment.Start && i < baseRtl.Count && baseRtl[i])
            {
                eff = TextAlignment.End;
            }
            if (eff is TextAlignment.Start or TextAlignment.Justify)
            {
                continue;
            }

            var line = lines[i];
            float delta = layoutWidth - line.Width;
            if (delta <= 0)
            {
                continue;
            }

            float newX = eff switch
            {
                TextAlignment.Center => delta / 2,
                TextAlignment.End => delta,
                _ => 0,
            };
            if (newX == 0)
            {
                continue;
            }

            var glyphs = (GlyphPosition[])line.Glyphs;
            var fontRuns = line.FontRuns is { } runs ? (FontRun[])runs : null;
            lines[i] = new LayoutLine(
                line.TextStart, line.TextLength,
                newX, line.Y,
                line.Width, line.AdvanceWidth, line.Height, line.Baseline,
                glyphs, fontRuns);
        }
    }

    static void ApplyAlignment(List<LayoutLine> lines, TextAlignment alignment, float layoutWidth)
    {
        if (alignment == TextAlignment.Start || layoutWidth <= 0)
        {
            return;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            float delta = layoutWidth - line.Width;
            if (delta <= 0)
            {
                continue;
            }

            float newX = alignment switch
            {
                TextAlignment.Center => delta / 2,
                TextAlignment.End    => delta,
                TextAlignment.Justify => 0, // Justify handled below
                _ => 0,
            };

            if (alignment == TextAlignment.Justify && i < lines.Count - 1)
            {
                // Don't justify the last line
                lines[i] = JustifyLine(line, layoutWidth);
            }
            else if (newX != 0)
            {
                // Only shift line.X — glyph positions stay relative to line origin.
                // DrawContext computes absolute position as (x + line.X) + glyph.X.
                var glyphs = (GlyphPosition[])line.Glyphs;
                var fontRuns = line.FontRuns is { } runs ? (FontRun[])runs : null;
                lines[i] = new LayoutLine(
                    line.TextStart, line.TextLength,
                    newX, line.Y,
                    line.Width, line.AdvanceWidth, line.Height, line.Baseline,
                    glyphs, fontRuns);
            }
        }
    }

    static LayoutLine JustifyLine(LayoutLine line, float layoutWidth)
    {
        if (line.Glyphs.Count < 2)
        {
            return line;
        }

        // Distribute extra space evenly between all glyphs.
        float extraSpace = layoutWidth - line.Width;
        if (extraSpace <= 0 || line.Glyphs.Count <= 1)
        {
            return line;
        }

        float gapPerGlyph = extraSpace / (line.Glyphs.Count - 1);
        var justified = new GlyphPosition[line.Glyphs.Count];
        for (int i = 0; i < line.Glyphs.Count; i++)
        {
            var glyph = line.Glyphs[i];
            justified[i] = new GlyphPosition(
                glyph.GlyphId,
                glyph.X + i * gapPerGlyph,
                glyph.Y,
                glyph.AdvanceWidth,
                glyph.ClusterIndex
            );
        }

        return new LayoutLine(
            line.TextStart, line.TextLength,
            0, line.Y,
            layoutWidth, layoutWidth, line.Height, line.Baseline,
            justified);
    }

    // ── Per-character font fallback ─────────────────────────────────────

    /// <summary>
    /// Scans all lines for .notdef glyphs (GlyphId == 0) and replaces them
    /// with glyphs shaped using a fallback font. Produces FontRun metadata
    /// so DrawContext can issue separate DrawGlyphs calls per font.
    /// </summary>
    static void ApplyFontFallback(List<LayoutLine> lines, string text, string primaryFontPath, float fontSize)
    {
        for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
        {
            var line = lines[lineIdx];
            if (line.Glyphs.Count == 0)
            {
                continue;
            }

            // Fast check: any .notdef glyphs?
            bool hasNotdef = false;
            for (int i = 0; i < line.Glyphs.Count; i++)
            {
                if (line.Glyphs[i].GlyphId == 0)
                {
                    hasNotdef = true;
                    break;
                }
            }

            if (!hasNotdef)
            {
                continue;
            }

            // Build replacement glyph array and font runs
            var newGlyphs = new GlyphPosition[line.Glyphs.Count];
            var fontRuns = new List<FontRun>();
            string currentFont = primaryFontPath;
            int runStart = 0;

            for (int i = 0; i < line.Glyphs.Count; i++)
            {
                var glyph = line.Glyphs[i];

                if (glyph.GlyphId != 0)
                {
                    // Primary font has this glyph
                    newGlyphs[i] = glyph;
                    if (currentFont != primaryFontPath)
                    {
                        fontRuns.Add(new FontRun(currentFont, runStart, i - runStart));
                        currentFont = primaryFontPath;
                        runStart = i;
                    }
                    continue;
                }

                // .notdef — find a fallback font for this character
                int clusterIndex = glyph.ClusterIndex;
                if (clusterIndex < 0 || clusterIndex >= text.Length)
                {
                    newGlyphs[i] = glyph;
                    continue;
                }

                int codepoint = char.ConvertToUtf32(text, clusterIndex);
                string? fallbackPath = FontFallback.FindFallbackFont(codepoint);

                if (fallbackPath == null)
                {
                    // No fallback available — keep .notdef
                    newGlyphs[i] = glyph;
                    continue;
                }

                // Shape this single character with the fallback font
                var fallbackShaper = GetOrCreateShaper(fallbackPath);
                string charStr = char.ConvertFromUtf32(codepoint);
                var fallbackShaped = fallbackShaper.Shape(charStr, fontSize);

                if (fallbackShaped.Glyphs.Length > 0 && fallbackShaped.Glyphs[0].GlyphId != 0)
                {
                    // Use the fallback glyph AND its own advance — the primary
                    // font's .notdef advance is the wrong width for the substitute
                    // (e.g. full-width CJK), and inheriting it makes glyphs overlap.
                    // X is re-flowed from the corrected advances after the loop.
                    var fb = fallbackShaped.Glyphs[0];
                    newGlyphs[i] = new GlyphPosition(
                        fb.GlyphId,
                        glyph.X,
                        glyph.Y,
                        fb.AdvanceWidth > 0 ? fb.AdvanceWidth : glyph.AdvanceWidth,
                        glyph.ClusterIndex
                    );

                    if (currentFont != fallbackPath)
                    {
                        if (i > runStart)
                        {
                            fontRuns.Add(new FontRun(currentFont, runStart, i - runStart));
                        }
                        currentFont = fallbackPath;
                        runStart = i;
                    }
                }
                else
                {
                    // Fallback font also doesn't have this glyph
                    newGlyphs[i] = glyph;
                }
            }

            // Close the final run
            if (line.Glyphs.Count > runStart)
            {
                fontRuns.Add(new FontRun(currentFont, runStart, line.Glyphs.Count - runStart));
            }

            // Only attach font runs if we actually used a fallback font
            bool hasFallback = false;
            foreach (var run in fontRuns)
            {
                if (run.FontPath != primaryFontPath)
                {
                    hasFallback = true;
                    break;
                }
            }

            // Re-flow X positions left-to-right by the (now-corrected) advances.
            // Substituting wide fallback glyphs changed advances, so positions
            // derived from the primary font's narrow .notdef advances would
            // overlap (e.g. dense CJK). For horizontal text xOffset is 0, so a
            // pen-position re-flow reproduces correct, non-overlapping spacing.
            float lineWidth = line.Width;
            float lineAdvanceWidth = line.AdvanceWidth;
            if (hasFallback && newGlyphs.Length > 0)
            {
                float pen = newGlyphs[0].X;
                for (int i = 0; i < newGlyphs.Length; i++)
                {
                    var g = newGlyphs[i];
                    newGlyphs[i] = new GlyphPosition(g.GlyphId, pen, g.Y, g.AdvanceWidth, g.ClusterIndex);
                    pen += g.AdvanceWidth;
                }
                lineWidth = pen;
                lineAdvanceWidth = pen;
            }

            lines[lineIdx] = new LayoutLine(
                line.TextStart, line.TextLength,
                line.X, line.Y,
                lineWidth, lineAdvanceWidth, line.Height, line.Baseline,
                newGlyphs,
                hasFallback ? fontRuns.ToArray() : null);
        }
    }
}

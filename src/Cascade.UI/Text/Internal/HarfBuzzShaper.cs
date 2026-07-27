using System.Buffers;
using Etch.Text.HarfBuzz;
using Etch.Text.Rasterize;
using Buffer = Etch.Text.HarfBuzz.Buffer;
using FontFace = Etch.Text.Shape.FontFace;

namespace Cascade.UI;

/// <summary>
/// Wraps the owned Etch.Text.HarfBuzz interop to shape text into positioned glyphs.
/// Thread-safe: a lock serializes access to the underlying HarfBuzz font.
/// </summary>
internal sealed class HarfBuzzShaper : IDisposable
{
    readonly Blob blob;
    readonly Face face;
    readonly Font font;
    readonly int unitsPerEm;
    readonly Lock shapeLock = new();

    // FreeType faces (one per pixel size) used only for ink-bounds measurement —
    // the rasterized bitmap is the ground truth for pixel-perfect centering,
    // whereas HarfBuzz's outline extents can disagree with the hinted bitmap by
    // up to ~0.4 px at small sizes. Loaded lazily; guarded by shapeLock.
    readonly Dictionary<float, FontFace?> inkFaces = [];
    byte[]? fontBytes;

    internal string FontPath { get; }

    internal HarfBuzzShaper(string fontPath)
    {
        FontPath = fontPath;
        blob = Blob.FromFile(fontPath);
        face = new Face(blob, 0);
        font = new Font(face);
        unitsPerEm = face.UnitsPerEm;
        if (unitsPerEm == 0)
        {
            unitsPerEm = 1000;
        }
        font.SetScale(unitsPerEm, unitsPerEm);
        font.SetFunctionsOpenType();
    }

    /// <summary>
    /// Shapes text into positioned glyphs at the given font size.
    /// Supports optional script/direction hints; when omitted HarfBuzz guesses.
    /// </summary>
    internal ShaperResult Shape(string text, float fontSize,
        Script script = default, Direction direction = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new ShaperResult([], 0f);
        }

        const float DpiScale = 96f / 72f;

        lock (shapeLock)
        {
            font.SetScale((int)(fontSize * 64), (int)(fontSize * 64));

            using var buffer = new Buffer();
            buffer.AddUtf16(text);

            if (script != default)
            {
                buffer.Script = script;
            }
            if (direction != default)
            {
                buffer.Direction = direction;
            }

            buffer.GuessSegmentProperties();
            font.Shape(buffer, Array.Empty<Feature>());

            var glyphInfos = buffer.GlyphInfos;
            var glyphPositions = buffer.GlyphPositions;
            var result = new GlyphPosition[glyphInfos.Length];

            float cursorX = 0;
            float cursorY = 0;

            for (int i = 0; i < glyphInfos.Length; i++)
            {
                float xOffset = glyphPositions[i].XOffset / 64f * DpiScale;
                float yOffset = glyphPositions[i].YOffset / 64f * DpiScale;
                float xAdvance = glyphPositions[i].XAdvance / 64f * DpiScale;
                float yAdvance = glyphPositions[i].YAdvance / 64f * DpiScale;

                result[i] = new GlyphPosition(
                    GlyphId:      glyphInfos[i].Codepoint,
                    X:            cursorX + xOffset,
                    Y:            cursorY + yOffset,
                    AdvanceWidth: xAdvance,
                    ClusterIndex: (int)glyphInfos[i].Cluster
                );

                cursorX += xAdvance;
                cursorY += yAdvance;
            }

            return new ShaperResult(result, cursorX);
        }
    }

    /// <summary>
    /// Shapes a single directional run with an explicit direction (WP-3521b bidi
    /// layout). RTL runs come back in visual (left-to-right) order with the same
    /// cursor convention as <see cref="Shape(string, float, Script, Direction)"/>.
    /// </summary>
    internal ShaperResult ShapeDirectional(string text, float fontSize, bool rightToLeft)
        => Shape(text, fontSize, default, rightToLeft ? Direction.RightToLeft : Direction.LeftToRight);

    /// <summary>
    /// Returns true if the font contains a glyph for the given Unicode codepoint.
    /// </summary>
    internal bool HasGlyph(int codepoint)
    {
        lock (shapeLock)
        {
            return font.TryGetGlyph((uint)codepoint, out uint glyph) && glyph != 0;
        }
    }

    /// <summary>
    /// Returns font metrics (ascender, descender, line gap) scaled to the requested size.
    /// </summary>
    internal FontMetrics GetMetrics(float fontSize)
    {
        const float DpiScale = 96f / 72f;
        lock (shapeLock)
        {
            font.SetScale((int)(fontSize * 64), (int)(fontSize * 64));
            if (font.TryGetHorizontalFontExtents(out var extents))
            {
                return new FontMetrics(
                    Ascender:  extents.Ascender / 64f * DpiScale,
                    Descender: extents.Descender / 64f * DpiScale,
                    LineGap:   extents.LineGap / 64f * DpiScale
                );
            }
            // Fallback: estimate from font size
            return new FontMetrics(
                Ascender:  fontSize * 0.8f * DpiScale,
                Descender: fontSize * -0.2f * DpiScale,
                LineGap:   0f
            );
        }
    }

    /// <summary>
    /// Returns the visual bounding box of a single glyph, accounting for the
    /// left side bearing that shifts the visual content from the pen position.
    /// Used for centering emoji and icon glyphs within cells.
    /// </summary>
    internal GlyphVisualBounds? GetGlyphVisualBounds(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        const float DpiScale = 96f / 72f;

        lock (shapeLock)
        {
            font.SetScale((int)(fontSize * 64), (int)(fontSize * 64));

            using var buffer = new Buffer();
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            font.Shape(buffer, Array.Empty<Feature>());

            var glyphInfos = buffer.GlyphInfos;
            if (glyphInfos.Length == 0)
            {
                return null;
            }

            uint glyphId = glyphInfos[0].Codepoint;
            if (!font.TryGetGlyphExtents(glyphId, out var extents))
            {
                return null;
            }

            var metrics = GetMetrics(fontSize);

            return new GlyphVisualBounds(
                XBearing:     extents.XBearing / 64f * DpiScale,
                VisualWidth:  extents.Width / 64f * DpiScale,
                VisualHeight: Math.Abs(extents.Height / 64f * DpiScale),
                AdvanceWidth: buffer.GlyphPositions[0].XAdvance / 64f * DpiScale,
                YBearing:     extents.YBearing / 64f * DpiScale,
                Ascent:       Math.Abs(metrics.Ascender)
            );
        }
    }

    /// <summary>
    /// Returns the bounding box of the <em>rasterized ink</em> of a text run,
    /// measured from the FreeType bitmap the renderer actually draws (not the
    /// HarfBuzz outline extents). This is the ground truth for pixel-perfect
    /// centering of short glyph runs (avatar initials, single-glyph badges),
    /// where the hinted bitmap's side bearings can differ from the outline by a
    /// fraction of a pixel and leave a visible off-centre lean.
    ///
    /// The result is shaped like <see cref="GetGlyphVisualBounds"/> and in the
    /// same 96/72 logical-pixel units, so callers can swap the measurement source
    /// without changing their centering math.
    /// </summary>
    internal GlyphVisualBounds? GetGlyphInkBounds(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        const float DpiScale = 96f / 72f;

        lock (shapeLock)
        {
            FontFace? inkFace = GetInkFace(fontSize);
            if (inkFace is null)
            {
                return null;
            }

            font.SetScale((int)(fontSize * 64), (int)(fontSize * 64));

            using var buffer = new Buffer();
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            font.Shape(buffer, Array.Empty<Feature>());

            var glyphInfos = buffer.GlyphInfos;
            var glyphPositions = buffer.GlyphPositions;
            if (glyphInfos.Length == 0)
            {
                return null;
            }

            // Union each glyph's ink, placed at its shaped pen offset. Baseline is
            // y = 0; screen Y grows downward, so ink above the baseline is
            // negative. Everything is expressed in 96/72 logical pixels: HarfBuzz
            // advances (raw pixel scale) are multiplied by DpiScale, while the
            // FreeType bitmap metrics already carry the 96/72 factor baked in by
            // FontFace.Load and so are used as-is.
            float penX = 0f;
            float inkLeft = float.MaxValue, inkRight = float.MinValue;
            float inkTop = float.MaxValue, inkBottom = float.MinValue;
            double totalCoverage = 0, weightedCentroidX = 0;
            bool any = false;

            for (int i = 0; i < glyphInfos.Length; i++)
            {
                if (TryMeasureGlyphInk(inkFace, (ushort)glyphInfos[i].Codepoint,
                        out int minX, out int minY, out int bmpHeight,
                        out int tightLeft, out int tightTop, out int inkW, out int inkH,
                        out float centroidInBitmapX, out double coverage))
                {
                    float glyphInkLeft = penX + minX + tightLeft;
                    float glyphInkTop = -(minY + bmpHeight) + tightTop;
                    inkLeft = MathF.Min(inkLeft, glyphInkLeft);
                    inkRight = MathF.Max(inkRight, glyphInkLeft + inkW);
                    inkTop = MathF.Min(inkTop, glyphInkTop);
                    inkBottom = MathF.Max(inkBottom, glyphInkTop + inkH);
                    // Coverage-weighted horizontal centre of mass from the pen origin.
                    totalCoverage += coverage;
                    weightedCentroidX += coverage * (penX + minX + centroidInBitmapX);
                    any = true;
                }

                penX += glyphPositions[i].XAdvance / 64f * DpiScale;
            }

            if (!any)
            {
                return null;
            }

            var metrics = GetMetrics(fontSize);

            return new GlyphVisualBounds(
                XBearing:     inkLeft,
                VisualWidth:  inkRight - inkLeft,
                VisualHeight: inkBottom - inkTop,
                AdvanceWidth: penX,
                YBearing:     -inkTop,
                Ascent:       Math.Abs(metrics.Ascender),
                CentroidX:    totalCoverage > 0 ? (float)(weightedCentroidX / totalCoverage) : float.NaN
            );
        }
    }

    /// <summary>Lazily loads (and caches) a FreeType face for ink measurement.</summary>
    FontFace? GetInkFace(float fontSize)
    {
        if (inkFaces.TryGetValue(fontSize, out var cached))
        {
            return cached;
        }

        FontFace? loaded = null;
        try
        {
            fontBytes ??= File.ReadAllBytes(FontPath);
            loaded = FontFace.Load(fontBytes, unitsPerEm, fontSize);
            // Match the renderer's hinting (EtchBackend uses Slight) so the ink
            // box measured here is the exact bitmap that gets drawn — Normal
            // (the FontFace default) grid-fits harder and yields a different
            // bitmap height at some sizes, throwing centring off by whole pixels.
            loaded.Hinting = Etch.Text.FontHinting.Slight;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            loaded = null;
        }

        inkFaces[fontSize] = loaded;
        return loaded;
    }

    /// <summary>
    /// Rasterizes one glyph and returns its bitmap origin plus the tight ink box
    /// (rows/cols with non-zero coverage). Returns false for empty glyphs (space).
    /// </summary>
    static bool TryMeasureGlyphInk(FontFace face, ushort glyphId,
        out int minX, out int minY, out int bmpHeight,
        out int tightLeft, out int tightTop, out int inkW, out int inkH,
        out float centroidInBitmapX, out double coverage)
    {
        minX = minY = bmpHeight = tightLeft = tightTop = inkW = inkH = 0;
        centroidInBitmapX = 0f;
        coverage = 0;

        GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, 0f);
        if (gw <= 0 || gh <= 0)
        {
            return false;
        }

        int bufSize = (gw + 1) * gh;
        byte[] buf = ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            buf.AsSpan(0, bufSize).Clear();
            GlyphRasterizer.Rasterize(face, glyphId, 0f, buf.AsSpan(0, bufSize),
                out int rw, out int rh, out minX, out minY);
            if (rw <= 0 || rh <= 0)
            {
                return false;
            }

            bmpHeight = rh;
            int top = -1, bottom = 0, left = -1, right = 0;
            double weightedX = 0;
            for (int row = 0; row < rh; row++)
            {
                int rowOffset = row * rw;
                for (int col = 0; col < rw; col++)
                {
                    byte c = buf[rowOffset + col];
                    if (c == 0)
                    {
                        continue;
                    }

                    coverage += c;
                    weightedX += c * (col + 0.5);

                    if (top < 0)
                    {
                        top = row;
                    }

                    bottom = row + 1;
                    if (left < 0 || col < left)
                    {
                        left = col;
                    }

                    if (col + 1 > right)
                    {
                        right = col + 1;
                    }
                }
            }

            if (top < 0)
            {
                return false;
            }

            tightTop = top;
            tightLeft = left;
            inkW = right - left;
            inkH = bottom - top;
            centroidInBitmapX = coverage > 0 ? (float)(weightedX / coverage) : rw / 2f;
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public void Dispose()
    {
        font.Dispose();
        face.Dispose();
        blob.Dispose();
        foreach (var inkFace in inkFaces.Values)
        {
            inkFace?.Dispose();
        }

        inkFaces.Clear();
    }
}

/// <summary>
/// Result of shaping a text string: an array of positioned glyphs and their total advance.
/// </summary>
internal readonly record struct ShaperResult(
    GlyphPosition[] Glyphs,
    float TotalAdvance
);

/// <summary>
/// Font vertical metrics scaled to a specific font size.
/// </summary>
internal readonly record struct FontMetrics(
    float Ascender,
    float Descender,
    float LineGap
);

/// <summary>
/// Visual bounding box of a glyph, including the left side bearing.
/// All values are in pixel units scaled to the requested font size.
/// </summary>
internal readonly record struct GlyphVisualBounds(
    float XBearing,
    float VisualWidth,
    float VisualHeight,
    float AdvanceWidth,
    float YBearing,
    float Ascent,
    float CentroidX = float.NaN
)
{
    /// <summary>
    /// Distance from the DrawText y position to the visual vertical center
    /// of the glyph. Use this for centering: ty = targetCenterY - VisualCenterY.
    /// </summary>
    public float VisualCenterY => Ascent - YBearing + VisualHeight / 2f;

    /// <summary>Bounding-box horizontal centre, measured from the pen origin.</summary>
    public float VisualCenterX => XBearing + VisualWidth / 2f;

    /// <summary>
    /// Coverage-weighted (optical) horizontal centre from the pen origin — the
    /// centre of the glyph's ink *mass*, which is what the eye reads as centred.
    /// Most letters carry more weight on one side than their bounding box implies
    /// (a "B" leans left of its box centre), so centring the box leaves the mass
    /// visibly off-centre. Falls back to the box centre when ink wasn't measured
    /// (<see cref="CentroidX"/> is NaN, e.g. the HarfBuzz-only path).
    /// </summary>
    public float OpticalCenterX => float.IsNaN(CentroidX) ? VisualCenterX : CentroidX;
}

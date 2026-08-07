using System.Numerics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI;

/// <summary>
/// The GPU-accelerated drawing surface for custom rendering. Provided to
/// Canvas node draw callbacks. All coordinate values are in logical pixels.
/// </summary>
/// <remarks>
/// Transform, clip, opacity, blend, blur, shadow, and color filter operations
/// return <see cref="IDisposable"/> scopes — use <c>using</c> blocks to ensure
/// they are properly popped from the render stack.
/// </remarks>
public sealed class DrawContext
{
    private EtchBackend? backend;
    private ulong frame;

    internal DrawContext()
    {
    }

    internal DrawContext(EtchBackend backend, ulong frame)
    {
        this.backend = backend;
        this.frame = frame;
    }

    /// <summary>
    /// Resets this context to target a new backend frame. Allows pooling the
    /// context across ticks to avoid per-frame allocations.
    /// </summary>
    internal void BeginFrame(EtchBackend backend, ulong frame, Size size, float pixelRatio, string? defaultFontPath)
    {
        this.backend = backend;
        this.frame = frame;
        Size = size;
        PixelRatio = pixelRatio;
        DefaultFontPath = defaultFontPath;
        fontExistsCache.Clear();
    }

    /// <summary>Internal access to the backend for layer texture retrieval.</summary>
    internal EtchBackend? Backend => backend;

    /// <summary>
    /// Tags subsequent backend draw commands with the emitting node's DevTools
    /// id. Called by the paint pass only when <see cref="DrawProvenance.CaptureEnabled"/>.
    /// </summary>
    internal void SetDrawProvenance(string? nodeId)
    {
        backend?.SetDrawProvenance(nodeId);
    }

    // ── Canvas properties ─────────────────────────────────────────────

    /// <summary>
    /// The canvas size in <b>device</b> pixels (the GPU frame surface size). Layout
    /// bounds and text measurements are in <b>logical</b> pixels, so for viewport math
    /// against them divide by <see cref="PixelRatio"/> (e.g. logical width =
    /// <c>Size.Width / PixelRatio</c>). At 1× DPI the two coincide.
    /// </summary>
    public Size Size { get; internal set; }

    /// <summary>
    /// The device pixel ratio. Use <c>1f / PixelRatio</c> for hairline stroke widths.
    /// </summary>
    public float PixelRatio { get; internal set; } = 1f;

    // ── Internal helpers ──────────────────────────────────────────────

    private static (int kind, GradientStop[] stops, float p0, float p1, float p2, float p3)
        PrepareGradient(Gradient gradient, Rect bounds)
    {
        var stops = new GradientStop[gradient.Stops.Count];
        for (int i = 0; i < gradient.Stops.Count; i++)
        {
            stops[i] = gradient.Stops[i];
        }

        float p0, p1, p2, p3;
        int kind;

        switch (gradient.GradientType)
        {
            case GradientKind.Linear:
                kind = 0;
                if (gradient.From != default || gradient.To != default)
                {
                    p0 = gradient.From.X;
                    p1 = gradient.From.Y;
                    p2 = gradient.To.X;
                    p3 = gradient.To.Y;
                }
                else
                {
                    float angleRad = gradient.Angle.InRadians;
                    float cx = bounds.X + bounds.Width / 2f;
                    float cy = bounds.Y + bounds.Height / 2f;
                    float hw = bounds.Width / 2f;
                    float hh = bounds.Height / 2f;
                    float dx = MathF.Cos(angleRad) * hw;
                    float dy = MathF.Sin(angleRad) * hh;
                    p0 = cx - dx;
                    p1 = cy - dy;
                    p2 = cx + dx;
                    p3 = cy + dy;
                }
                break;

            case GradientKind.Radial:
                kind = 1;
                p0 = gradient.Center.X;
                p1 = gradient.Center.Y;
                p2 = gradient.GradientRadius;
                p3 = 0f;
                break;

            case GradientKind.Sweep:
                kind = 2;
                p0 = gradient.Center.X;
                p1 = gradient.Center.Y;
                p2 = gradient.Angle.InRadians;
                p3 = gradient.Angle.InRadians + MathF.PI * 2f;
                break;

            default:
                kind = 0;
                p0 = p1 = p2 = p3 = 0f;
                break;
        }

        return (kind, stops, p0, p1, p2, p3);
    }

    private static Rect ComputePathBounds(Path path)
    {
        var data = path.Data.Span;
        if (data.Length < 2)
        {
            return default;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        var cmds = path.Commands.Span;
        int di = 0;
        for (int i = 0; i < cmds.Length; i++)
        {
            int floats = cmds[i] switch
            {
                Path.CmdMoveTo => 2,
                Path.CmdLineTo => 2,
                Path.CmdCubicTo => 6,
                Path.CmdQuadTo => 4,
                _ => 0,
            };

            for (int f = 0; f < floats; f += 2)
            {
                if (di + f + 1 < data.Length)
                {
                    float x = data[di + f];
                    float y = data[di + f + 1];
                    if (x < minX) { minX = x; }
                    if (y < minY) { minY = y; }
                    if (x > maxX) { maxX = x; }
                    if (y > maxY) { maxY = y; }
                }
            }

            di += floats;
        }

        if (minX > maxX)
        {
            return default;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private void DrawPathInternal(Path path, ColorValue? fill, Stroke? stroke)
    {
        ulong compiled = backend!.CompilePath(path.Commands.Span, path.Data.Span);
        try
        {
            ColorValue? strokeColor = stroke?.Color;
            float strokeWidth = stroke?.Width ?? 0f;

            backend.DrawPath(frame, compiled, fill, strokeColor, strokeWidth,
                stroke?.Cap ?? StrokeCap.Butt,
                stroke?.Join ?? StrokeJoin.Miter);
        }
        finally
        {
            backend.DestroyPath(compiled);
        }
    }

    // ScopeGuard lives in its own file (Rendering/ScopeGuard.cs) as a
    // public readonly struct so that Push* methods can return it without
    // any heap allocation. A `default` ScopeGuard is a safe no-op on
    // Dispose, allowing callers to declare scope locals up front and
    // assign conditionally without paying for a closure or display class.

    // ── Primitives ────────────────────────────────────────────────────

    /// <summary>Draws a rectangle with optional fill, stroke, and corner radius.</summary>
    public void DrawRect(Rect rect, ColorValue? fill = null, Stroke? stroke = null, float radius = 0)
    {
        if (backend is null)
        {
            return;
        }

        ColorValue? strokeColor = stroke?.Color;
        float strokeWidth = stroke?.Width ?? 0f;

        backend.DrawRect(frame, rect.X, rect.Y, rect.Width, rect.Height, radius,
            fill, strokeColor, strokeWidth);
    }

    /// <summary>Draws a rectangle with a gradient fill.</summary>
    public void DrawRect(Rect rect, Gradient fill, float radius = 0)
    {
        if (backend is null)
        {
            return;
        }

        var (kind, stops, p0, p1, p2, p3) = PrepareGradient(fill, rect);
        backend.DrawRectGradient(frame, rect.X, rect.Y, rect.Width, rect.Height, radius,
            kind, stops, p0, p1, p2, p3);
    }

    /// <summary>
    /// Draws a blurred rounded rectangle. Used for box shadows, glow effects,
    /// and soft UI elements. The blur is Gaussian with the given standard deviation.
    /// </summary>
    /// <param name="rect">Position and size of the rectangle.</param>
    /// <param name="color">Fill color of the blurred shape.</param>
    /// <param name="radius">Corner radius of the underlying rounded rectangle.</param>
    /// <param name="blurSigma">Standard deviation of the Gaussian blur in logical pixels.</param>
    public void DrawBlurredRoundedRect(Rect rect, ColorValue color, float radius = 0, float blurSigma = 4f)
    {
        if (backend is null || color.A <= 0f)
        {
            return;
        }

        if (blurSigma < 0.75f)
        {
            DrawRect(rect, fill: color, radius: radius);
            return;
        }

        // The GPU backend has no render-to-texture blur pass, so approximate a
        // Gaussian drop shadow by stacking translucent rounded rects that grow
        // outward with Gaussian-weighted alpha (drawn largest/faintest first, so
        // they composite into a soft falloff). This uses the normal rounded-rect
        // primitive, so ordering, clipping and scene caching all work, and it reads
        // as a smooth shadow at this layer count.
        const int layers = 12;
        float sigma = blurSigma;
        float extent = sigma * 3f;       // shadow reaches ~3σ past the shape edge
        float baseA = color.A;

        for (int k = layers; k >= 1; k--)
        {
            float grow = extent * k / layers;
            float weight = MathF.Exp(-(grow * grow) / (2f * sigma * sigma));
            float layerA = baseA * weight * (1.7f / layers);
            if (layerA < 0.002f)
            {
                continue;
            }

            var lr = new Rect(rect.X - grow, rect.Y - grow, rect.Width + 2f * grow, rect.Height + 2f * grow);
            DrawRect(lr, fill: color.Opacity(Math.Clamp(layerA, 0f, 1f)), radius: radius + grow);
        }

        // Solid core at the shape footprint — the element the shadow sits behind
        // covers most of it; its edge keeps the shadow solid right at the shape.
        DrawRect(rect, fill: color, radius: radius);
    }

    /// <summary>
    /// Frosted-glass backdrop blur: fills a rounded rect with a Gaussian blur of
    /// whatever is already drawn behind it, then applies <paramref name="tint"/> over
    /// the blur. Draw this instead of a solid panel background (don't also fill the
    /// rect opaquely, or there'd be nothing behind to blur). Content drawn later
    /// (text, icons) renders on top.
    /// </summary>
    /// <param name="rect">Panel bounds.</param>
    /// <param name="tint">Tint applied over the blur (its alpha = tint strength).</param>
    /// <param name="radius">Corner radius.</param>
    /// <param name="blurSigma">Gaussian blur radius (standard deviation).</param>
    public void DrawBackdropBlur(Rect rect, ColorValue tint, float radius = 0f, float blurSigma = 12f)
    {
        if (backend is null)
        {
            return;
        }

        backend.DrawBackdropBlur(frame, rect.X, rect.Y, rect.Width, rect.Height, radius, blurSigma, tint);
    }

    /// <summary>Draws a circle with optional fill and stroke.</summary>
    public void DrawCircle(Point center, float radius, ColorValue? fill = null, Stroke? stroke = null)
    {
        if (backend is null || radius <= 0f)
        {
            return;
        }

        ColorValue? strokeColor = stroke?.Color;
        float strokeWidth = stroke?.Width ?? 0f;
        StrokeCap cap = stroke?.Cap ?? StrokeCap.Butt;
        StrokeJoin join = stroke?.Join ?? StrokeJoin.Miter;

        backend.DrawCircle(frame, center.X, center.Y, radius,
            fill, strokeColor, strokeWidth, cap, join);
    }

    /// <summary>Draws a filled annular sector (pie or donut slice).</summary>
    public void DrawSector(Point center, float outerRadius, float innerRadius,
        float startRad, float sweepRad, ColorValue fill)
    {
        if (backend is null || outerRadius <= 0f || MathF.Abs(sweepRad) < 0.001f)
        {
            return;
        }

        backend.DrawSector(frame, center.X, center.Y, outerRadius,
            innerRadius, startRad, sweepRad, fill);
    }

    /// <summary>Draws an ellipse bounded by the given rectangle.</summary>
    public void DrawEllipse(Rect bounds, ColorValue? fill = null, Stroke? stroke = null)
    {
        if (backend is null)
        {
            return;
        }

        var center = bounds.Center;
        float rx = bounds.Width / 2f;
        float ry = bounds.Height / 2f;
        float maxR = MathF.Max(rx, ry);

        if (maxR <= 0f)
        {
            return;
        }

        using (PushTranslate(center.X, center.Y))
        using (PushScale(rx / maxR, ry / maxR))
        {
            var path = Path.Circle(new Point(0, 0), maxR);
            DrawPathInternal(path, fill, stroke);
        }
    }

    /// <summary>Draws a line between two points.</summary>
    /// <remarks>
    /// Uses the backend's stroked-line fast path — no managed path allocation.
    /// </remarks>
    public void DrawLine(Point from, Point to, Stroke stroke)
    {
        if (backend is null || stroke.Width <= 0f)
        {
            return;
        }

        backend.DrawLine(
            frame,
            from.X, from.Y, to.X, to.Y,
            stroke.Color, stroke.Width,
            stroke.Cap, stroke.Join);
    }

    /// <summary>Draws an arc (partial circle outline).</summary>
    /// <remarks>
    /// Uses the backend's stroked-arc fast path — no managed path allocation.
    /// </remarks>
    public void DrawArc(Point center, float radius, Angle startAngle, Angle sweepAngle, Stroke stroke)
    {
        if (backend is null || stroke.Width <= 0f)
        {
            return;
        }

        backend.DrawArc(
            frame,
            center.X, center.Y, radius,
            startAngle.InRadians, sweepAngle.InRadians,
            stroke.Color, stroke.Width,
            stroke.Cap, stroke.Join);
    }

    // ── Paths ─────────────────────────────────────────────────────────

    /// <summary>Draws a path with optional fill and stroke.</summary>
    public void DrawPath(Path path, ColorValue? fill = null, Stroke? stroke = null)
    {
        if (backend is null)
        {
            return;
        }

        DrawPathInternal(path, fill, stroke);
    }

    /// <summary>Draws a path with a gradient fill and optional stroke.</summary>
    public void DrawPath(Path path, Gradient fill, Stroke? stroke = null)
    {
        if (backend is null)
        {
            return;
        }

        var bounds = ComputePathBounds(path);
        var (kind, stops, p0, p1, p2, p3) = PrepareGradient(fill, bounds);

        ulong compiled = backend.CompilePath(path.Commands.Span, path.Data.Span);
        try
        {
            ColorValue? strokeColor = stroke?.Color;

            backend.DrawPathGradient(frame, compiled, kind, stops, p0, p1, p2, p3,
                strokeColor,
                stroke?.Width ?? 0f,
                stroke?.Cap ?? StrokeCap.Butt,
                stroke?.Join ?? StrokeJoin.Miter);
        }
        finally
        {
            backend.DestroyPath(compiled);
        }
    }

    // ── Images ────────────────────────────────────────────────────────

    /// <summary>Draws an image scaled to the destination rectangle.</summary>
    public void DrawImage(ImageSource image, Rect dest, float opacity = 1f)
    {
        if (backend is null)
        {
            return;
        }

        ulong gpuHandle = image.EnsureUploaded(backend);
        if (gpuHandle == 0)
        {
            return;
        }

        backend.DrawImage(frame, gpuHandle, dest.X, dest.Y, dest.Width, dest.Height, opacity);
    }

    /// <summary>Draws a sub-region of an image to the destination rectangle.</summary>
    public void DrawImage(ImageSource image, Rect source, Rect dest, float opacity = 1f)
    {
        if (backend is null)
        {
            return;
        }

        ulong gpuHandle = image.EnsureUploaded(backend);
        if (gpuHandle == 0)
        {
            return;
        }

        // Implement sub-image drawing via clip + transform:
        // Scale the full image so that the source rect maps to the dest rect,
        // then clip to the dest rect.
        float scaleX = dest.Width / source.Width;
        float scaleY = dest.Height / source.Height;
        float fullW = image.Width * scaleX;
        float fullH = image.Height * scaleY;
        float offsetX = dest.X - source.X * scaleX;
        float offsetY = dest.Y - source.Y * scaleY;

        using (PushClip(dest))
        {
            backend.DrawImage(frame, gpuHandle, offsetX, offsetY, fullW, fullH, opacity);
        }
    }

    // ── Text ──────────────────────────────────────────────────────────

    /// <summary>
    /// The default font path used for text rendering when no explicit font is provided.
    /// Set by the framework during initialization based on the active theme's font family.
    /// </summary>
    internal string? DefaultFontPath { get; set; }

    /// <summary>Font handle cache — maps font file paths to backend-loaded font handles.</summary>
    private readonly Dictionary<string, ulong> fontHandleCache = new();

    /// <summary>Cache of resolved weight-specific font paths.</summary>
    private readonly Dictionary<(string, FontWeight), string> weightPathCache = new();

    /// <summary>
    /// Cached File.Exists results for font paths. Font paths are a small bounded
    /// set (typically 3-10 per app lifetime) and don't disappear at runtime, so
    /// this is safe to cache statically across all DrawContext instances. Avoids
    /// per-paint syscall + path-normalization allocation.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> fontExistsCache = new();

    /// <summary>Cached existence check for a font path.</summary>
    private static bool FontExists(string path)
    {
        if (fontExistsCache.TryGetValue(path, out bool exists))
        {
            return exists;
        }
        exists = File.Exists(path);
        fontExistsCache[path] = exists;
        return exists;
    }

    /// <summary>
    /// Resolves a font path for the given weight, caching results.
    /// Falls back to the base path if no weight variant exists.
    /// </summary>
    internal string ResolveFontPath(string baseFontPath, FontWeight weight)
    {
        if (weight is FontWeight.Regular or FontWeight.None)
        {
            return baseFontPath;
        }

        var key = (baseFontPath, weight);
        if (weightPathCache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        string resolved = FontFallback.ResolveFontForWeight(baseFontPath, weight);
        weightPathCache[key] = resolved;
        return resolved;
    }

    /// <summary>Measures the rendered size of text without drawing it.</summary>
    /// <remarks>
    /// Uses the TextLayoutEngine for accurate measurement with HarfBuzz shaping
    /// when a font path is available, falling back to a character-width approximation
    /// when no font is loaded (e.g., in headless testing).
    /// </remarks>
    public Size MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Zero;
        }

        return MeasureText(text, 14f);
    }

    /// <summary>Measures text with the specified font size.</summary>
    public Size MeasureText(string text, float fontSize, string? fontPath = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Zero;
        }

        string? resolvedFont = fontPath ?? DefaultFontPath;
        if (resolvedFont != null && FontExists(resolvedFont))
        {
            var options = new TextLayoutOptions
            {
                FontPath = resolvedFont,
                FontSize = fontSize,
                MaxWidth = float.PositiveInfinity,
            };
            var result = TextLayoutEngine.Layout(text, options);
            return result.BoundingBox;
        }

        // Approximate fallback when no font is available (headless/testing)
        float avgCharWidthRatio = 0.6f;
        float lineHeightRatio = 1.2f;
        float width = text.Length * fontSize * avgCharWidthRatio;
        float height = fontSize * lineHeightRatio;
        return new Size(width, height);
    }

    /// <summary>
    /// Measures text advance width including trailing whitespace.
    /// Use this for caret positioning where the cursor must advance past trailing spaces.
    /// </summary>
    public Size MeasureTextAdvance(string text, float fontSize, string? fontPath = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Zero;
        }

        string? resolvedFont = fontPath ?? DefaultFontPath;
        if (resolvedFont != null && FontExists(resolvedFont))
        {
            var options = new TextLayoutOptions
            {
                FontPath = resolvedFont,
                FontSize = fontSize,
                MaxWidth = float.PositiveInfinity,
            };
            var result = TextLayoutEngine.Layout(text, options);
            return result.AdvanceBox;
        }

        // Approximate fallback when no font is available (headless/testing)
        float avgCharWidthRatio = 0.6f;
        float lineHeightRatio = 1.2f;
        float width = text.Length * fontSize * avgCharWidthRatio;
        float height = fontSize * lineHeightRatio;
        return new Size(width, height);
    }

    /// <summary>
    /// Returns the visual bounding box of a glyph, including the left side bearing
    /// that offsets the visual rendering from the pen position. Used for precise
    /// centering of emoji and icon glyphs within cells.
    /// </summary>
    internal GlyphVisualBounds? MeasureGlyphVisualBounds(string text, float fontSize, string? fontPath = null)
    {
        string? resolvedFont = fontPath ?? DefaultFontPath;
        if (string.IsNullOrEmpty(text) || resolvedFont == null || !FontExists(resolvedFont))
        {
            return null;
        }

        return TextLayoutEngine.GetGlyphVisualBounds(text, fontSize, resolvedFont);
    }

    /// <summary>
    /// Returns the FreeType-rasterized ink bounding box of a short glyph run — the
    /// bitmap the renderer actually draws — for pixel-perfect centering of single
    /// glyphs / avatar initials. Falls back to null (caller should use
    /// <see cref="MeasureGlyphVisualBounds"/> or advance-box centring) in headless
    /// mode or when the font is unavailable.
    /// </summary>
    internal GlyphVisualBounds? MeasureGlyphInkBounds(string text, float fontSize, string? fontPath = null)
    {
        string? resolvedFont = fontPath ?? DefaultFontPath;
        if (string.IsNullOrEmpty(text) || resolvedFont == null || !FontExists(resolvedFont))
        {
            return null;
        }

        return TextLayoutEngine.GetGlyphInkBounds(text, fontSize, resolvedFont);
    }

    /// <summary>Draws text at the specified position.</summary>
    /// <remarks>
    /// Uses the TextLayoutEngine (HarfBuzz shaping, UAX #14 line breaking, alignment)
    /// to produce glyph positions, then sends them to the GPU backend for rendering.
    /// When no backend is available (headless mode), this is a no-op.
    /// </remarks>
    public void DrawText(string text, float x, float y, float fontSize,
        ColorValue color, string? fontPath = null,
        TextAlignment alignment = TextAlignment.Start,
        TextOverflow overflow = TextOverflow.Clip,
        float maxWidth = float.PositiveInfinity,
        int maxLines = 0)
    {
        if (backend is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        string? resolvedFont = fontPath ?? DefaultFontPath;
        if (resolvedFont == null || !FontExists(resolvedFont))
        {
            return;
        }

        var options = new TextLayoutOptions
        {
            FontPath = resolvedFont,
            FontSize = fontSize,
            MaxWidth = maxWidth,
            Alignment = alignment,
            Overflow = overflow,
            MaxLines = maxLines,
        };

        var layout = TextLayoutEngine.Layout(text, options);
        if (layout.LinesArray.Length == 0)
        {
            return;
        }

        ulong fontHandle = GetOrLoadFont(resolvedFont);
        if (fontHandle == 0)
        {
            return;
        }

        // Snap line origin and baseline to the pixel grid for crisp
        // rendering, but preserve HarfBuzz's relative glyph offsets so
        // kerning and spacing stay accurate. Rounding each glyph X
        // independently destroys inter-character spacing.

        // Zero-alloc iteration over internal arrays.
        var linesArray = layout.LinesArray;
        for (int li = 0; li < linesArray.Length; li++)
        {
            var line = linesArray[li];
            var glyphsArray = line.GlyphsArray;
            if (glyphsArray.Length == 0)
            {
                continue;
            }

            // Line origin X. For a single line, do NOT snap: horizontal glyph
            // placement already bakes a quarter-pixel subpixel offset into the
            // bitmap (GlyphPlacement — the quad sits at the floored device pen and
            // the fraction lives in the rasterised bucket), so a fractional origin
            // is placed accurately to ¼ device px. Snapping the origin first — to
            // either grid — discards up to half a pixel of a *centred* position
            // that the buckets cannot recover, leaving single glyphs (avatar
            // initials) leaning off-centre. Multi-line keeps the logical snap so
            // wrapped lines share an identical integer left edge.
            float lineOriginX = linesArray.Length == 1
                ? x + line.X
                : MathF.Round(x + line.X);

            // Baseline snapping. Multi-line text snaps here (logical grid) to keep
            // wrapped-line spacing uniform. A *single* line does NOT snap here: the
            // glyph placement primitive (GlyphPlacement.QuadOriginY) already rounds
            // the baseline to the nearest physical pixel row in *absolute device*
            // space, downstream of the transform. Snapping here as well is a double
            // snap in the wrong coordinate space — this method sees only local
            // coordinates, but a control's transform adds a fractional absolute
            // device offset (e.g. an avatar at device y 343.4), so a local snap
            // lands off the absolute grid and, after QuadOriginY re-rounds, can push
            // a vertically-centred glyph a whole device pixel off centre. Leaving
            // the single-line baseline fractional lets QuadOriginY do the one true
            // device-space snap, which centres to the ½-device-px grid floor.
            float rawBaseline = y + line.Y + line.Baseline;
            float baselineY = linesArray.Length == 1
                ? rawBaseline
                : MathF.Round(rawBaseline);

            var fontRunsArray = line.FontRunsArray;
            if (fontRunsArray is null)
            {
                // Fast path: single font for the entire line.
                // Use stackalloc for small glyph runs (the overwhelmingly
                // common case for UI text) and ArrayPool for long runs.
                DrawGlyphRun(fontHandle, glyphsArray, 0, glyphsArray.Length,
                    lineOriginX, baselineY, fontSize, color);
            }
            else
            {
                // Multi-font line: issue separate DrawGlyphs per font run
                for (int ri = 0; ri < fontRunsArray.Length; ri++)
                {
                    var run = fontRunsArray[ri];
                    if (run.GlyphCount == 0)
                    {
                        continue;
                    }

                    ulong runFontHandle = run.FontPath == resolvedFont
                        ? fontHandle
                        : GetOrLoadFont(run.FontPath);
                    if (runFontHandle == 0)
                    {
                        continue;
                    }

                    DrawGlyphRun(runFontHandle, glyphsArray, run.GlyphStartIndex, run.GlyphCount,
                        lineOriginX, baselineY, fontSize, color);
                }
            }
        }
    }

    /// <summary>
    /// Emits a glyph run to the backend with zero managed allocation.
    /// Uses stackalloc for small runs (the common case) and ArrayPool for
    /// longer ones, keeping the paint pass allocation-free.
    /// </summary>
    private void DrawGlyphRun(
        ulong fontHandle,
        GlyphPosition[] glyphs,
        int glyphStart, int glyphCount,
        float lineOriginX, float baselineY,
        float fontSize, ColorValue color)
    {
        if (backend is null || glyphCount == 0)
        {
            return;
        }

        const int StackThreshold = 128;

        if (glyphCount <= StackThreshold)
        {
            Span<ushort> glyphIds = stackalloc ushort[StackThreshold];
            Span<float> positions = stackalloc float[StackThreshold * 2];
            var idSlice = glyphIds.Slice(0, glyphCount);
            var posSlice = positions.Slice(0, glyphCount * 2);
            for (int i = 0; i < glyphCount; i++)
            {
                var glyph = glyphs[glyphStart + i];
                idSlice[i] = (ushort)glyph.GlyphId;
                posSlice[i * 2] = lineOriginX + glyph.X;
                posSlice[i * 2 + 1] = baselineY + glyph.Y;
            }
            backend.DrawGlyphs(frame, fontHandle, idSlice, posSlice, fontSize, color);
        }
        else
        {
            var rentedIds = System.Buffers.ArrayPool<ushort>.Shared.Rent(glyphCount);
            var rentedPositions = System.Buffers.ArrayPool<float>.Shared.Rent(glyphCount * 2);
            try
            {
                var idSpan = rentedIds.AsSpan(0, glyphCount);
                var posSpan = rentedPositions.AsSpan(0, glyphCount * 2);
                for (int i = 0; i < glyphCount; i++)
                {
                    var glyph = glyphs[glyphStart + i];
                    idSpan[i] = (ushort)glyph.GlyphId;
                    posSpan[i * 2] = lineOriginX + glyph.X;
                    posSpan[i * 2 + 1] = baselineY + glyph.Y;
                }
                backend.DrawGlyphs(frame, fontHandle, idSpan, posSpan, fontSize, color);
            }
            finally
            {
                System.Buffers.ArrayPool<ushort>.Shared.Return(rentedIds);
                System.Buffers.ArrayPool<float>.Shared.Return(rentedPositions);
            }
        }
    }

    /// <summary>Gets or loads a font handle for the given font path.</summary>
    private ulong GetOrLoadFont(string fontPath)
    {
        if (fontHandleCache.TryGetValue(fontPath, out ulong cached))
        {
            return cached;
        }

        if (backend is null)
        {
            return 0;
        }

        try
        {
            byte[] fontData = File.ReadAllBytes(fontPath);
            ulong handle = backend.LoadFont(fontData, 0);
            fontHandleCache[fontPath] = handle;
            return handle;
        }
        catch
        {
            return 0;
        }
    }

    // ── Transforms (scoped) ───────────────────────────────────────────

    /// <summary>Pushes an arbitrary 3x2 matrix transform.</summary>
    public ScopeGuard PushTransform(Matrix3x2 transform)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushTransform(frame, transform);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Transform);
    }

    /// <summary>Pushes a translation transform.</summary>
    public ScopeGuard PushTranslate(float x, float y)
    {
        return PushTransform(Matrix3x2.CreateTranslation(x, y));
    }

    /// <summary>Pushes a scale transform around an optional origin point.</summary>
    public ScopeGuard PushScale(float x, float y, Point? origin = null)
    {
        if (origin.HasValue)
        {
            var o = origin.Value;
            return PushTransform(Matrix3x2.CreateScale(x, y, new Vector2(o.X, o.Y)));
        }

        return PushTransform(Matrix3x2.CreateScale(x, y));
    }

    /// <summary>Pushes a rotation transform around an optional origin point.</summary>
    public ScopeGuard PushRotate(Angle angle, Point? origin = null)
    {
        if (origin.HasValue)
        {
            var o = origin.Value;
            return PushTransform(Matrix3x2.CreateRotation(angle.InRadians, new Vector2(o.X, o.Y)));
        }

        return PushTransform(Matrix3x2.CreateRotation(angle.InRadians));
    }

    /// <summary>Pushes a skew transform.</summary>
    public ScopeGuard PushSkew(float x, float y)
    {
        return PushTransform(Matrix3x2.CreateSkew(x, y));
    }

    // ── Clipping (scoped) ─────────────────────────────────────────────

    /// <summary>Pushes a rectangular clip region.</summary>
    public ScopeGuard PushClip(Rect rect)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushClip(frame, rect.X, rect.Y, rect.Width, rect.Height);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Clip);
    }

    /// <summary>Pushes a path-based clip region.</summary>
    public ScopeGuard PushClip(Path path)
    {
        if (backend is null)
        {
            return default;
        }

        ulong compiled = backend.CompilePath(path.Commands.Span, path.Data.Span);
        backend.PushClipPath(frame, compiled);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.ClipPath, compiled);
    }

    /// <summary>Pushes a rounded rectangle clip region.</summary>
    public ScopeGuard PushRoundedClip(Rect rect, float radius)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushClipRoundedRect(frame, rect.X, rect.Y, rect.Width, rect.Height, radius);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Clip);
    }

    // ── Opacity and blending (scoped) ─────────────────────────────────

    /// <summary>
    /// Sets the per-frame pixel-dissolve threshold [0,1]. Fragments whose
    /// screen-space hash noise is below the threshold are discarded, so raising it
    /// scatters the frame away pixel by pixel. 0 disables it. Used by the dissolve
    /// page transition; global for the frame, so only paint the dissolving content.
    /// </summary>
    internal void SetFrameDissolve(float threshold)
    {
        if (backend is not null)
        {
            backend.FrameDissolve = Math.Clamp(threshold, 0f, 1f);
        }
    }

    /// <summary>Pushes an opacity modifier for the enclosed scope.</summary>
    public ScopeGuard PushOpacity(float opacity)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushLayer(frame, opacity, BlendMode.Normal);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    /// <summary>Pushes a blend mode for the enclosed scope.</summary>
    public ScopeGuard PushBlendMode(BlendMode mode)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushLayer(frame, 1f, mode);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    // ── Filters (scoped) ──────────────────────────────────────────────

    /// <summary>Pushes a Gaussian blur filter.</summary>
    /// <remarks>
    /// Pushes a compositing layer for content isolation. General content blur
    /// requires a render-to-texture + compute pass not yet wired into this path.
    /// For box shadow blur, use <see cref="DrawBlurredRoundedRect"/> or
    /// <see cref="PushDropShadow"/> instead. Content within this scope renders
    /// correctly in an isolated layer.
    /// </remarks>
    public ScopeGuard PushBlur(float sigma)
    {
        if (backend is null)
        {
            return default;
        }

        _ = sigma;
        backend.PushLayer(frame, 1f, BlendMode.Normal);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    /// <summary>Pushes a drop shadow filter.</summary>
    /// <remarks>
    /// Draws a GPU-accelerated blurred rounded rectangle as a shadow behind
    /// the content using Etch's native Gaussian blur support. The shadow
    /// is drawn at the bounds of the current clip region offset by the shadow
    /// parameters. Content within the scope renders on top of the shadow.
    /// </remarks>
    public ScopeGuard PushDropShadow(ShadowValue shadow)
    {
        if (backend is null)
        {
            return default;
        }

        // Draw the shadow as a blurred rounded rectangle behind the content.
        // The caller is expected to draw content on top within the scope.
        // We convert CSS-style blur radius (full width) to Gaussian sigma
        // (standard deviation) by dividing by 2.
        float sigma = shadow.BlurRadius / 2f;
        if (sigma > 0)
        {
            var shadowRect = new Rect(
                shadow.OffsetX - shadow.SpreadRadius,
                shadow.OffsetY - shadow.SpreadRadius,
                Size.Width + shadow.SpreadRadius * 2,
                Size.Height + shadow.SpreadRadius * 2);

            DrawBlurredRoundedRect(shadowRect, shadow.Color, radius: 0f, blurSigma: sigma);
        }

        backend.PushLayer(frame, 1f, BlendMode.Normal);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    /// <summary>Pushes a color filter transformation.</summary>
    /// <remarks>
    /// Pushes a compositing layer for content isolation. Color matrix
    /// transformation requires custom shader support not yet wired into this path.
    /// Content within the scope renders correctly in an isolated layer.
    /// </remarks>
    public ScopeGuard PushColorFilter(ColorFilter filter)
    {
        if (backend is null)
        {
            return default;
        }

        _ = filter;
        backend.PushLayer(frame, 1f, BlendMode.Normal);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    // ── Layers (scoped) ───────────────────────────────────────────────

    /// <summary>
    /// Pushes a compositing layer. All drawing within the scope is rendered
    /// to an offscreen surface, then composited with the given opacity and
    /// blend mode.
    /// </summary>
    public ScopeGuard PushLayer(float opacity = 1f, BlendMode? blend = null)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushLayer(frame, opacity, blend ?? BlendMode.Normal);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.Layer);
    }

    // ── Overlay capture (for popups that must render on top) ─────────

    /// <summary>
    /// Begins capturing subsequent text and glyph commands into an overlay
    /// buffer that will be rendered on top of the main frame text.
    /// Geometry commands continue to go to the main frame.
    /// </summary>
    public void PushOverlay()
    {
        backend?.PushOverlay(frame);
    }

    /// <summary>
    /// Ends overlay capture.
    /// </summary>
    public void PopOverlay()
    {
        backend?.PopOverlay(frame);
    }

    /// <summary>
    /// Marks the point at which deferred-overlay (popup) painting begins, so the presenter can cull
    /// main-frame images that an overlay covers (preventing e.g. a list row's icon from bleeding
    /// through an open dropdown). Call once, immediately before painting the deferred overlays.
    /// </summary>
    public void MarkOverlayStart()
    {
        backend?.MarkOverlayStart();
    }

    // ── Layer texture compositing (Flutter-style retained layers) ────

    /// <summary>
    /// Allocates a stable, globally-unique layer handle for a retained layer. Callers
    /// allocate once (per ScrollView) and reuse it across recaptures so the layer's
    /// cache key never collides with another layer's. See <see cref="EtchBackend.NextLayerHandle"/>.
    /// </summary>
    public ulong NextLayerHandle() => backend?.NextLayerHandle() ?? 0;

    /// <summary>
    /// Begins capturing all subsequent draw commands into an offscreen texture under
    /// the given (caller-owned, stable) handle. Returns a scope that must be disposed
    /// to end the capture. The texture has the specified logical size.
    /// </summary>
    public ScopeGuard PushLayerTexture(ulong handle, float width, float height)
    {
        if (backend is null)
        {
            return default;
        }

        backend.PushLayerTexture(frame, handle, width, height);
        return new ScopeGuard(backend, frame, ScopeGuard.Kind.LayerTexture, handle);
    }

    /// <summary>
    /// Draws a previously captured layer texture into the current frame,
    /// offset by (x, y) in logical pixels. This is the fast path for scroll:
    /// the layer content is already rasterized; only the offset changes.
    /// </summary>
    public void DrawLayerTexture(ulong layerHandle, float x, float y, float opacity = 1f)
    {
        if (backend is null)
        {
            return;
        }

        backend.DrawLayerTexture(frame, layerHandle, x, y, opacity);
    }
}

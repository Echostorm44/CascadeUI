namespace Cascade.UI;

/// <summary>
/// Runtime gate for draw-provenance capture (WP-3505 observability tools).
/// When enabled, the paint pass tags every backend draw command with the
/// DevTools node id of the node that emitted it, and the render backend
/// retains a queryable snapshot of the last presented frame's draws.
/// Enabled lazily by the first whodrew/instances MCP query, so apps that are
/// never asked pay nothing — not even in debug builds.
/// </summary>
internal static class DrawProvenance
{
    private static volatile bool captureEnabled;

    /// <summary>
    /// Whether provenance tagging and per-frame draw snapshots are active.
    /// Read on the UI thread every frame; written by the MCP handler thread.
    /// Once enabled it stays enabled for the process lifetime.
    /// </summary>
    internal static bool CaptureEnabled
    {
        get => captureEnabled;
        set => captureEnabled = value;
    }
}

/// <summary>
/// GPU pass names used in <see cref="ShapeDrawRecord.Pass"/> and the whodrew
/// tool output. Constants so the producer (backend provider) and consumer
/// (MCP handlers) cannot drift on a typo.
/// </summary>
internal static class DrawPassNames
{
    internal const string Geometry = "geometry";
    internal const string Image = "image";
    internal const string Glyph = "glyph";
}

/// <summary>
/// One shape/image draw from the last presented frame, in device-space
/// coordinates with paint order preserved. Produced by the render backend
/// provider when <see cref="DrawProvenance.CaptureEnabled"/> is set.
/// Struct on purpose: thousands are rebuilt per presented frame.
/// </summary>
/// <param name="Pass">Which pipeline drew it: "geometry" or "image". Shapes,
/// images and glyphs are drawn in paint order (see <paramref name="PaintOrder"/>).</param>
/// <param name="Kind">Op kind: rect, rect_gradient, circle, line, arc, path,
/// path_gradient, sector, image.</param>
/// <param name="Fill">Fill color, or null for stroke-only ops.</param>
/// <param name="Stroke">Stroke color, or null for fill-only ops.</param>
/// <param name="NodeId">DevTools node id of the emitting node, or null when
/// the draw was issued outside the node paint pass.</param>
/// <param name="OpIndex">Index of the op in its command list — preserves
/// relative paint order within a pass.</param>
/// <param name="LayerHandle">Layer texture handle the op was captured into,
/// or 0 for the main frame. Layer bounds already include the scroll offset.</param>
/// <param name="PaintOrder">Position in the frame's paint order (see
/// <see cref="DrawPaintOrder"/>); records that overlap draw in ascending order.</param>
internal readonly record struct ShapeDrawRecord(
    string Pass,
    string Kind,
    float MinX,
    float MinY,
    float MaxX,
    float MaxY,
    ColorValue? Fill,
    ColorValue? Stroke,
    string? NodeId,
    int OpIndex,
    ulong LayerHandle,
    long PaintOrder);

/// <summary>
/// One glyph quad from the last presented frame, exactly as uploaded to the
/// GPU glyph pass (device-space position/size, atlas texel rect, color, clip).
/// Struct on purpose: rebuilt per presented frame while capture is enabled.
/// </summary>
/// <param name="AtlasU">Atlas texel rect of the glyph bitmap (texels, not
/// normalized UVs) — pair with the atlas capture tool to inspect the bitmap.</param>
/// <param name="Category">Where the run was painted: "main" (the frame's own
/// command stream) or "layer" (a retained ScrollView layer).</param>
/// <param name="NodeId">DevTools node id of the emitting node, or null.</param>
/// <param name="PaintOrder">Position in the frame's paint order (see
/// <see cref="DrawPaintOrder"/>).</param>
internal readonly record struct GlyphDrawRecord(
    float X,
    float Y,
    float Width,
    float Height,
    ushort GlyphId,
    float FontSize,
    ulong FontHandle,
    float AtlasU,
    float AtlasV,
    float AtlasW,
    float AtlasH,
    ColorValue Color,
    float ClipMinX,
    float ClipMinY,
    float ClipMaxX,
    float ClipMaxY,
    bool HasClip,
    bool IsColorGlyph,
    string Category,
    string? NodeId,
    long PaintOrder);

/// <summary>
/// Sort keys that put shape, image and glyph records in the order the GPU presenter draws them:
/// the frame's command stream, with each retained layer's own stream spliced in at the
/// <c>DrawLayerTexture</c> op that composites it. A glyph run painted when its stream held
/// <c>c</c> commands draws after command <c>c − 1</c> and before command <c>c</c>.
/// </summary>
internal static class DrawPaintOrder
{
    /// <summary>Main-stream command <paramref name="index"/>.</summary>
    internal static long MainCommand(int index) => Compose(2L * index + 1, 0);

    /// <summary>Main-stream glyph run painted when the stream held <paramref name="commandIndex"/> commands.</summary>
    internal static long MainGlyphRun(int commandIndex) => Compose(2L * commandIndex, 0);

    /// <summary>Command <paramref name="layerIndex"/> of a layer composited at main command <paramref name="mainIndex"/>.</summary>
    internal static long LayerCommand(int mainIndex, int layerIndex) => Compose(2L * mainIndex + 1, 2L * layerIndex + 2);

    /// <summary>Glyph run of a layer composited at main command <paramref name="mainIndex"/>.</summary>
    internal static long LayerGlyphRun(int mainIndex, int commandIndex) => Compose(2L * mainIndex + 1, 2L * commandIndex + 1);

    private static long Compose(long main, long layer) => (main << 32) | layer;
}

/// <summary>
/// Immutable snapshot of every draw in the last presented frame. The lists
/// are private copies — safe to read from any thread without locking.
/// </summary>
/// <param name="UncapturedLayers">Number of composited retained layers whose
/// content was captured before provenance capture enabled — their draws are
/// missing from <paramref name="Shapes"/> until the layer re-captures.
/// Surfaced in tool output when non-zero so an answer is never silently
/// incomplete.</param>
internal sealed record DrawSnapshot(
    long Frame,
    int AtlasDimension,
    IReadOnlyList<ShapeDrawRecord> Shapes,
    IReadOnlyList<GlyphDrawRecord> Glyphs,
    int UncapturedLayers);

/// <summary>
/// GPU readback of a glyph-atlas region. The image is the region expanded to
/// RGBA (the atlas itself is single-channel coverage).
/// </summary>
internal sealed record AtlasRegionCapture(
    ImageData Image,
    int AtlasDimension,
    int U,
    int V,
    int Width,
    int Height);

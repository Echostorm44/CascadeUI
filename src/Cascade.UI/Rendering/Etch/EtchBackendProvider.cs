using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Cascade.UI;
using Cascade.UI.Diagnostics;
using Etch.Scene;
using Etch.Testing;
using EGeometry = Etch.Geometry;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Information needed to render a cached layer texture during compositing.
/// </summary>
internal readonly struct LayerRenderInfo
{
    public readonly ulong LayerHandle;
    public readonly SceneBuffer Scene;
    public readonly float OffsetX;
    public readonly float OffsetY;
    public readonly float Opacity;
    public readonly IReadOnlyList<EtchBackend.GlyphOp> GlyphCommands;

    /// <summary>
    /// The layer's <c>DrawImage</c> ops paired with the full local→device transform
    /// in effect where each was captured (the layer's initial transform composed with
    /// any intra-layer <c>PushTransform</c>s — the same chain the layer's shapes are
    /// baked through). Unlike glyphs, whose positions are baked to absolute device
    /// coords at capture time, image ops store raw rects, so the presenter applies
    /// this transform then the scroll delta to composite them. Empty when the layer
    /// draws no images.
    /// </summary>
    public readonly IReadOnlyList<(EtchBackend.SceneOp Op, Matrix3x2 Transform)> ImageCommands;

    /// <summary>
    /// Screen-space viewport clip the layer is composited into (the ScrollView
    /// viewport), or null when the layer is drawn unclipped. The presenter
    /// intersects composited layer shapes and glyphs with this so retained
    /// layer content does not bleed outside its viewport (WP-3517).
    /// </summary>
    public readonly Cascade.UI.Rect? ViewportClip;

    public LayerRenderInfo(ulong handle, SceneBuffer scene, float x, float y, float opacity,
        IReadOnlyList<EtchBackend.GlyphOp> glyphCommands,
        IReadOnlyList<(EtchBackend.SceneOp Op, Matrix3x2 Transform)> imageCommands,
        Cascade.UI.Rect? viewportClip)
    {
        LayerHandle = handle;
        Scene = scene;
        OffsetX = x;
        OffsetY = y;
        Opacity = opacity;
        GlyphCommands = glyphCommands;
        ImageCommands = imageCommands;
        ViewportClip = viewportClip;
    }
}

internal sealed class EtchBackendProvider : IDisposable
{
    private readonly EtchBackend _backend;
    private nint _hwnd;
    private uint _width;
    private uint _height;
    private bool _disposed;
    private EtchGpuPresenter? _etchGpuPresenter = null;
    private bool _useGpu;
    private bool _forceCpuFallback;
    // WP-3519: persisted glyph text-weight gamma, reapplied if the presenter is
    // recreated (resize). Defaults to the perceptual weight; 0 = legacy linear.
    private float _textGamma = EtchGpuPresenter.DefaultTextGamma;
    // WP-3537: persisted light-on-dark weight factor (0 = linear, 1 = full weight).
    private float _lightWeight = EtchGpuPresenter.DefaultLightWeight;

    // WP-3513: retains the last CPU-rendered frame (RGBA, native buffer size)
    // when there is no GPU presenter, so CaptureFrame() — and therefore
    // `mcp screenshot` — works on a no-GPU machine. One reused buffer; no
    // per-frame allocation in the steady state.
    private byte[]? _cpuCaptureBuffer;
    private int _cpuCaptureWidth;
    private int _cpuCaptureHeight;

    // Reusable transform/clip state for AppendSceneOp — avoids per-build allocations
    // and is shared (sequentially) by the live frame and each retained-layer build.
    private readonly SceneAppendState _appendState = new();

    // SceneBuffer cache — avoid rebuilding when commands and viewport are unchanged
    private SceneBuffer? _cachedSceneBuffer;
    private ulong _cachedSceneHash;
    private ColorValue _cachedBaseColor;
    private uint _cachedWidth;
    private uint _cachedHeight;
    private float _cachedScale;
    private List<LayerRenderInfo> _cachedActiveLayers = new();

    // Layer SceneBuffer cache — each layer is cached independently by its command hash.
    // Also preserves glyph and image commands (plus the layer's initial transform) so
    // EtchGpuPresenter can re-rasterize text and re-blit icons during scroll even when
    // LayerCaptures has been cleared by Reset().
    private readonly Dictionary<ulong, (SceneBuffer Scene, ulong Hash,
        List<EtchBackend.GlyphOp> GlyphCommands,
        List<(EtchBackend.SceneOp Op, Matrix3x2 Transform)> ImageCommands)> _cachedLayerScenes = new();

    // Active layers for the current frame — passed to EtchGpuPresenter for compositing
    private readonly List<LayerRenderInfo> _activeLayers = new();

    public EtchBackendProvider()
    {
        _backend = new EtchBackend();
    }

    public EtchBackend Backend => _backend;

    /// <summary>
    /// The device (physical) framebuffer size in pixels — the resolution a GPU
    /// readback / screenshot is captured at, before any vision-API downscale.
    /// Used to map returned-screenshot coordinates back to logical input space.
    /// </summary>
    public (int Width, int Height) DeviceSize => ((int)_width, (int)_height);

    public void CreateSurface(nint windowHandle, uint width, uint height)
    {
        _hwnd = windowHandle;
        _width = width;
        _height = height;

        // CASCADE_FORCE_CPU=1 simulates GPU init failure so the CPU fallback
        // (including the GDI blit path) is exercisable on machines with a
        // working GPU.
        if (Environment.GetEnvironmentVariable("CASCADE_FORCE_CPU") == "1")
        {
            _useGpu = false;
            return;
        }

        try
        {
            _etchGpuPresenter = new EtchGpuPresenter(windowHandle, width, height);
            _etchGpuPresenter.TextGamma = _textGamma;
            _etchGpuPresenter.LightWeight = _lightWeight;
            _useGpu = true;
            NativeMemorySnapshotProvider.Register(() => _etchGpuPresenter.GetNativeMemorySnapshot());
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "etch-gpu-init.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:O}] GPU init failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
            _useGpu = false;
        }
    }

    public void CreateSurfaceX11(nint display, uint window, int screen, uint width, uint height)
        => (_width, _height) = (width, height);

    public void CreateSurfaceWayland(nint display, nint wlSurface, uint width, uint height)
        => (_width, _height) = (width, height);

    public void ResizeSurface(uint width, uint height)
    {
        _width = width;
        _height = height;
        _etchGpuPresenter?.Resize(width, height);
    }

    public (ulong frameHandle, uint width, uint height) BeginFrame(uint width, uint height)
    {
        _width = width;
        _height = height;
        _backend.Width = width;
        _backend.Height = height;
        _backend.Reset();
        return (1, width, height);
    }

    // PresentFrame error log throttle — a persistent per-frame failure must not
    // grow the error log without bound (the log itself stays unconditional so
    // real failures are visible without any env var).
    private int presentErrorCount;
    private const int MaxPresentErrorLogEntries = 100;

    // RENDER-001 fail-loud guard. The live and layer render paths each dispatch on
    // SceneOp kind; a kind one of them does not handle used to drop silently (that is
    // how the retained-layer image gap shipped invisibly). This sink asserts in Debug
    // (so a parity gap fails tests and dev builds immediately) and throttled-logs in
    // Release, so the next missed primitive is a loud failure, never a missing pixel.
    private int unhandledOpCount;
    private const int MaxUnhandledOpLogEntries = 50;

    private void ReportUnhandledOp(EtchBackend.OpKind kind, string context)
    {
        System.Diagnostics.Debug.Assert(false,
            $"Unhandled SceneOp {kind} in {context} — live/layer render parity gap (RENDER-001).");
        unhandledOpCount++;
        if (unhandledOpCount > MaxUnhandledOpLogEntries)
        {
            return;
        }
        var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "etch-backend-error.log");
        System.IO.File.AppendAllText(path,
            $"[{DateTime.Now:O}] Unhandled SceneOp {kind} in {context} — render parity gap (RENDER-001)\n");
        if (unhandledOpCount == MaxUnhandledOpLogEntries)
        {
            System.IO.File.AppendAllText(path,
                $"[{DateTime.Now:O}] {MaxUnhandledOpLogEntries} unhandled-op entries logged — further entries suppressed\n");
        }
    }

    // CASCADE_CAPTURE=<path>: write the latest presented frame to a PNG (overwritten each
    // present). A headless/CI-friendly way to prove the window actually rendered (not just
    // "didn't crash"); overwriting-latest handles reactive UIs that present a few frames then
    // idle, so the file always reflects the last thing actually shown.
    private string? capturePath;
    private bool capturePathResolved;

    public void PresentFrame(ulong frameHandle, ColorValue baseColor)
    {
        try
        {
            PresentFrameCore(frameHandle, baseColor);
            MaybeCaptureToFile();
        }
        catch (Exception ex)
        {
            presentErrorCount++;
            if (presentErrorCount > MaxPresentErrorLogEntries)
            {
                return;
            }
            var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "etch-backend-error.log");
            // ex.ToString() includes the full InnerException chain — essential for TypeInitializationException,
            // whose real cause (native dll not found / bad image / missing transitive dep) is only in the inner.
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:O}] {ex}\n\n");
            if (presentErrorCount == MaxPresentErrorLogEntries)
            {
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:O}] {MaxPresentErrorLogEntries} present errors logged — further entries suppressed\n\n");
            }
        }
    }

    private void MaybeCaptureToFile()
    {
        if (!capturePathResolved)
        {
            capturePath = Environment.GetEnvironmentVariable("CASCADE_CAPTURE");
            capturePathResolved = true;
        }
        if (string.IsNullOrEmpty(capturePath))
        {
            return;
        }
        try
        {
            ImageData? frame = CaptureFrame();
            if (frame is not null)
            {
                System.IO.File.WriteAllBytes(capturePath, Cascade.UI.AiImage.EncodePng(frame));
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
    }

    public ImageData? CaptureFrame()
    {
        var gpuFrame = _etchGpuPresenter?.CaptureFrame();
        if (gpuFrame is not null)
        {
            return gpuFrame;
        }

        // No GPU presenter (CASCADE_FORCE_CPU=1 or real GPU init failure):
        // serve the retained CPU frame at its native (reduced) buffer size.
        // It is the 640-capped render until WP-3514 lands native-res CPU
        // fidelity; do not upscale here — a screenshot must not fake detail.
        if (_cpuCaptureBuffer is not null && _cpuCaptureWidth > 0 && _cpuCaptureHeight > 0)
        {
            return new ImageData
            {
                Pixels = _cpuCaptureBuffer,
                Width = _cpuCaptureWidth,
                Height = _cpuCaptureHeight,
                Stride = _cpuCaptureWidth * 4,
            };
        }

        return null;
    }

    public void RequestCapture()
    {
        _etchGpuPresenter?.RequestCapture();
    }

    public void SetRenderParam(string param, string value)
    {
        switch (param)
        {
            case "render_mode":
                _forceCpuFallback = value.Equals("cpu", StringComparison.OrdinalIgnoreCase);
                break;
            case "textgamma":
                // WP-3519 prototype: 0 = legacy linear blend, 1.5 = macOS-ish weight.
                if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g))
                {
                    _textGamma = g;
                    _backend.TextGamma = g;
                    if (_etchGpuPresenter is not null)
                    {
                        _etchGpuPresenter.TextGamma = g;
                    }
                }
                break;
            case "textlightweight":
                // WP-3537: adaptive light-weight strength. 1 = full adaptive (weight
                // derived from fg-vs-bg contrast; default), 0 = legacy symmetric weight.
                if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lw))
                {
                    _lightWeight = lw;
                    _backend.LightWeight = lw;
                    if (_etchGpuPresenter is not null)
                    {
                        _etchGpuPresenter.LightWeight = lw;
                    }
                }
                break;
        }
    }

    // ── Draw snapshot (WP-3505 observability) ───────────────────────

    private readonly object drawSnapshotGate = new();
    private List<ShapeDrawRecord> snapshotShapes = new();
    private List<GlyphDrawRecord> snapshotGlyphs = new();
    private long snapshotFrame;

    // Back buffers refilled on each captured present, then swapped under the
    // gate so queries never observe a half-built frame.
    private List<ShapeDrawRecord> pendingShapes = new();
    private List<GlyphDrawRecord> pendingGlyphs = new();

    // Shape records for layer content in layer-local space, keyed by layer
    // handle. During scroll the layer content is not re-emitted (only the
    // DrawLayerTexture offset changes), so records persist here and the
    // offset is applied at assembly time each frame. Layer handles are
    // reused across frames (the backend resets its counter every frame), so
    // entries not referenced by the current frame are pruned after assembly.
    private readonly Dictionary<ulong, List<ShapeDrawRecord>> layerShapeRecords = new();
    private readonly HashSet<ulong> liveLayerHandles = new();
    private readonly List<ulong> deadLayerHandles = new();

    // Composited layers whose content predates capture enablement — their
    // draws are missing from the snapshot until the layer re-captures.
    private int pendingUncapturedLayers;
    private int snapshotUncapturedLayers;

    // Reused replay state for AppendShapeRecords.
    private readonly Stack<Matrix3x2> recordTransformStack = new();
    private readonly Stack<(float MinX, float MinY, float MaxX, float MaxY)> recordClipStack = new();

    public DrawSnapshot? GetDrawSnapshot()
    {
        lock (drawSnapshotGate)
        {
            if (snapshotFrame == 0)
            {
                return null;
            }

            return new DrawSnapshot(
                snapshotFrame,
                _etchGpuPresenter?.AtlasDimension ?? 0,
                snapshotShapes.ToArray(),
                snapshotGlyphs.ToArray(),
                snapshotUncapturedLayers);
        }
    }

    public AtlasRegionCapture? CaptureAtlasRegion(int u, int v, int width, int height)
    {
        return _etchGpuPresenter?.CaptureAtlasRegion(u, v, width, height);
    }

    /// <summary>
    /// Publishes the draw records built during this present. No-op when the
    /// present did not happen (e.g. swapchain acquire failure) — the previous
    /// snapshot stays valid for its own frame number.
    /// </summary>
    private void PublishDrawSnapshot(long presentBaseline)
    {
        long presented = PresentMonitor.PresentedFrames;
        if (presented == presentBaseline)
        {
            return;
        }

        pendingShapes.Clear();
        BuildShapeRecords(pendingShapes);

        lock (drawSnapshotGate)
        {
            (snapshotShapes, pendingShapes) = (pendingShapes, snapshotShapes);
            (snapshotGlyphs, pendingGlyphs) = (pendingGlyphs, snapshotGlyphs);
            snapshotUncapturedLayers = pendingUncapturedLayers;
            snapshotFrame = presented;
        }
    }

    private void BuildShapeRecords(List<ShapeDrawRecord> target)
    {
        pendingUncapturedLayers = 0;
        liveLayerHandles.Clear();

        // Refresh layer-local records for every layer captured this frame.
        // Layers that did not re-emit (scroll frames) keep their cached
        // records; the offset is applied below when splicing.
        foreach (var (handle, capture) in _backend.LayerCaptures)
        {
            if (!layerShapeRecords.TryGetValue(handle, out var records))
            {
                records = new List<ShapeDrawRecord>();
                layerShapeRecords[handle] = records;
            }

            records.Clear();
            AppendShapeRecords(capture.Commands, records, capture.InitialTransform, handle, spliceLayers: false);
            liveLayerHandles.Add(handle);
        }

        AppendShapeRecords(_backend.Commands, target, Matrix3x2.Identity, layerHandle: 0, spliceLayers: true);

        // Drop cached records for layers the current frame neither captured
        // nor composited — handles are reused, so stale entries would
        // misattribute the next layer that gets the same handle.
        deadLayerHandles.Clear();
        foreach (ulong handle in layerShapeRecords.Keys)
        {
            if (!liveLayerHandles.Contains(handle))
            {
                deadLayerHandles.Add(handle);
            }
        }
        foreach (ulong handle in deadLayerHandles)
        {
            layerShapeRecords.Remove(handle);
        }
    }

    /// <summary>
    /// Replays a backend command list into device-space draw records,
    /// mirroring the transform/clip semantics of <see cref="BuildSceneBuffer"/>:
    /// transforms compose onto the current matrix, clip bounds are the
    /// axis-aligned intersection of the clip stack.
    /// </summary>
    private void AppendShapeRecords(
        IReadOnlyList<EtchBackend.SceneOp> commands,
        List<ShapeDrawRecord> target,
        Matrix3x2 initialTransform,
        ulong layerHandle,
        bool spliceLayers)
    {
        recordTransformStack.Clear();
        recordClipStack.Clear();
        Matrix3x2 current = initialTransform;
        (float MinX, float MinY, float MaxX, float MaxY) clip =
            (float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);

        for (int i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            switch (cmd.Kind)
            {
                case EtchBackend.OpKind.PushTransform:
                    recordTransformStack.Push(current);
                    current = cmd.Matrix * current;
                    break;

                case EtchBackend.OpKind.PopTransform:
                    if (recordTransformStack.Count > 0)
                    {
                        current = recordTransformStack.Pop();
                    }
                    break;

                case EtchBackend.OpKind.PushClip:
                case EtchBackend.OpKind.PushClipRoundedRect:
                {
                    recordClipStack.Push(clip);
                    var bounds = TransformBounds(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H, current);
                    clip = IntersectClip(clip, bounds);
                    break;
                }

                case EtchBackend.OpKind.PushClipPath:
                {
                    recordClipStack.Push(clip);
                    var path = _backend.GetCompiledPath(cmd.PathHandle);
                    if (path.HasValue)
                    {
                        var aabb = path.Value.Aabb();
                        var bounds = TransformBounds(
                            (float)aabb.MinX, (float)aabb.MinY, (float)aabb.MaxX, (float)aabb.MaxY, current);
                        clip = IntersectClip(clip, bounds);
                    }
                    break;
                }

                case EtchBackend.OpKind.PopClip:
                    if (recordClipStack.Count > 0)
                    {
                        clip = recordClipStack.Pop();
                    }
                    break;

                case EtchBackend.OpKind.DrawLayerTexture:
                    // Handle is stored in W, offset in X/Y (device space) —
                    // same convention BuildSceneBuffer reads. WP-3517: the GPU
                    // path clips composited layer content to the viewport, so
                    // the whodrew splice intersects each offset record with the
                    // same clip and drops records fully outside it — keeping the
                    // provenance answer faithful to what actually painted.
                    if (spliceLayers)
                    {
                        ulong layerTextureHandle = (ulong)cmd.W;
                        liveLayerHandles.Add(layerTextureHandle);
                        if (layerShapeRecords.TryGetValue(layerTextureHandle, out var layerRecords))
                        {
                            float clipMinX = cmd.HasClipBounds ? cmd.ClipBounds.X : float.MinValue;
                            float clipMinY = cmd.HasClipBounds ? cmd.ClipBounds.Y : float.MinValue;
                            float clipMaxX = cmd.HasClipBounds ? cmd.ClipBounds.X + cmd.ClipBounds.Width : float.MaxValue;
                            float clipMaxY = cmd.HasClipBounds ? cmd.ClipBounds.Y + cmd.ClipBounds.Height : float.MaxValue;
                            // RENDER-003: the records are baked at absolute coords through the
                            // layer's InitialTransform (which carries the ScrollView's on-screen
                            // origin), and the composite offset (cmd.X/Y) carries it too — so the
                            // residual shift is the scroll delta offset − clipOrigin, exactly as
                            // the GPU path applies (EtchGpuPresenter.LayerScrollDelta). Adding the
                            // whole offset double-counted the origin and dropped layer-canvas
                            // shapes ~origin pixels away from where they actually painted.
                            float spliceOffX = cmd.HasClipBounds ? cmd.X - cmd.ClipBounds.X : cmd.X;
                            float spliceOffY = cmd.HasClipBounds ? cmd.Y - cmd.ClipBounds.Y : cmd.Y;
                            foreach (var record in layerRecords)
                            {
                                float minX = Math.Max(record.MinX + spliceOffX, clipMinX);
                                float minY = Math.Max(record.MinY + spliceOffY, clipMinY);
                                float maxX = Math.Min(record.MaxX + spliceOffX, clipMaxX);
                                float maxY = Math.Min(record.MaxY + spliceOffY, clipMaxY);
                                if (maxX <= minX || maxY <= minY)
                                {
                                    continue;
                                }
                                target.Add(record with
                                {
                                    MinX = minX,
                                    MinY = minY,
                                    MaxX = maxX,
                                    MaxY = maxY,
                                });
                            }
                        }
                        else
                        {
                            // The layer's content was captured before
                            // provenance capture enabled — its draws are
                            // missing. Surfaced as uncaptured_layers in tool
                            // output so the answer is never silently partial.
                            pendingUncapturedLayers++;
                        }
                    }
                    break;

                default:
                    if (TryGetDrawBounds(cmd, out string kind, out string pass,
                        out float localMinX, out float localMinY, out float localMaxX, out float localMaxY))
                    {
                        var bounds = TransformBounds(localMinX, localMinY, localMaxX, localMaxY, current);
                        float minX = Math.Max(bounds.MinX, clip.MinX);
                        float minY = Math.Max(bounds.MinY, clip.MinY);
                        float maxX = Math.Min(bounds.MaxX, clip.MaxX);
                        float maxY = Math.Min(bounds.MaxY, clip.MaxY);
                        if (minX <= maxX && minY <= maxY)
                        {
                            target.Add(new ShapeDrawRecord(
                                pass, kind, minX, minY, maxX, maxY,
                                cmd.Fill, cmd.StrokeColor, cmd.DebugNodeId, i, layerHandle));
                        }
                    }
                    break;
            }
        }
    }

    private bool TryGetDrawBounds(EtchBackend.SceneOp cmd, out string kind, out string pass,
        out float minX, out float minY, out float maxX, out float maxY)
    {
        pass = DrawPassNames.Geometry;
        switch (cmd.Kind)
        {
            case EtchBackend.OpKind.DrawRect:
                kind = "rect";
                (minX, minY, maxX, maxY) = (cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
                return true;

            case EtchBackend.OpKind.DrawRectGradient:
                kind = "rect_gradient";
                (minX, minY, maxX, maxY) = (cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
                return true;

            case EtchBackend.OpKind.DrawCircle:
                kind = "circle";
                (minX, minY, maxX, maxY) = (cmd.X - cmd.Radius, cmd.Y - cmd.Radius, cmd.X + cmd.Radius, cmd.Y + cmd.Radius);
                return true;

            case EtchBackend.OpKind.DrawArc:
                kind = "arc";
                float arcExpand = cmd.Radius + cmd.StrokeWidth * 0.5f;
                (minX, minY, maxX, maxY) = (cmd.X - arcExpand, cmd.Y - arcExpand, cmd.X + arcExpand, cmd.Y + arcExpand);
                return true;

            case EtchBackend.OpKind.DrawSector:
                kind = "sector";
                (minX, minY, maxX, maxY) = (cmd.X - cmd.Radius, cmd.Y - cmd.Radius, cmd.X + cmd.Radius, cmd.Y + cmd.Radius);
                return true;

            case EtchBackend.OpKind.DrawLine:
            {
                kind = "line";
                float half = cmd.StrokeWidth * 0.5f;
                minX = Math.Min(cmd.X, cmd.W) - half;
                minY = Math.Min(cmd.Y, cmd.H) - half;
                maxX = Math.Max(cmd.X, cmd.W) + half;
                maxY = Math.Max(cmd.Y, cmd.H) + half;
                return true;
            }

            case EtchBackend.OpKind.DrawPath:
            case EtchBackend.OpKind.DrawPathGradient:
            {
                kind = cmd.Kind == EtchBackend.OpKind.DrawPath ? "path" : "path_gradient";
                var path = _backend.GetCompiledPath(cmd.PathHandle);
                if (!path.HasValue)
                {
                    (minX, minY, maxX, maxY) = (0, 0, 0, 0);
                    return false;
                }
                var aabb = path.Value.Aabb();
                if (aabb.IsEmpty)
                {
                    (minX, minY, maxX, maxY) = (0, 0, 0, 0);
                    return false;
                }
                (minX, minY, maxX, maxY) = ((float)aabb.MinX, (float)aabb.MinY, (float)aabb.MaxX, (float)aabb.MaxY);
                return true;
            }

            case EtchBackend.OpKind.DrawImage:
                kind = "image";
                pass = DrawPassNames.Image;
                (minX, minY, maxX, maxY) = (cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
                return true;

            default:
                kind = "";
                (minX, minY, maxX, maxY) = (0, 0, 0, 0);
                return false;
        }
    }

    private static (float MinX, float MinY, float MaxX, float MaxY) TransformBounds(
        float minX, float minY, float maxX, float maxY, Matrix3x2 matrix)
    {
        if (matrix.IsIdentity)
        {
            return (minX, minY, maxX, maxY);
        }

        var tl = Vector2.Transform(new Vector2(minX, minY), matrix);
        var tr = Vector2.Transform(new Vector2(maxX, minY), matrix);
        var bl = Vector2.Transform(new Vector2(minX, maxY), matrix);
        var br = Vector2.Transform(new Vector2(maxX, maxY), matrix);
        return (
            Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X)),
            Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y)),
            Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X)),
            Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y)));
    }

    private static (float MinX, float MinY, float MaxX, float MaxY) IntersectClip(
        (float MinX, float MinY, float MaxX, float MaxY) a,
        (float MinX, float MinY, float MaxX, float MaxY) b)
    {
        return (
            Math.Max(a.MinX, b.MinX),
            Math.Max(a.MinY, b.MinY),
            Math.Min(a.MaxX, b.MaxX),
            Math.Min(a.MaxY, b.MaxY));
    }

    private void PresentFrameCore(ulong frameHandle, ColorValue baseColor)
    {
        bool captureDraws = DrawProvenance.CaptureEnabled;
        long captureBaseline = captureDraws ? PresentMonitor.PresentedFrames : 0;
        if (captureDraws)
        {
            pendingGlyphs.Clear();
        }

        var gpuScene = BuildSceneBuffer(baseColor);

        bool canRenderGpu = EtchGpuPresenter.CanRenderGpu(gpuScene);
        if (DebugLog.IsEnabled(DebugLogCategory.Present))
        {
            DebugLog.Write(DebugLogCategory.Present,
                $"[{DateTime.Now:O}] PresentFrameCore: forceCpu={_forceCpuFallback}, presenter={_etchGpuPresenter != null}, canRenderGpu={canRenderGpu}, layers={_activeLayers.Count}");
        }

        if (!_forceCpuFallback && _etchGpuPresenter != null && canRenderGpu)
        {
            _etchGpuPresenter.PresentScene(gpuScene, _backend, _activeLayers, captureDraws ? pendingGlyphs : null);
            if (captureDraws)
            {
                PublishDrawSnapshot(captureBaseline);
            }
            return;
        }

        // WP-3514: the single-threaded CPU rasterizer renders this full scene at
        // native resolution in ~12.8 s/frame (measured: HelloCascade, 1920x1080)
        // — far too slow per frame — so the cap stays for GEOMETRY, which is then
        // upscaled, while TEXT is rasterized at native resolution in a second
        // pass. Text was the worst of the upscale blur; geometry tolerates the
        // stretch. (A faster native path would need an O(n) classified-scene
        // renderer; tracked separately.)
        const int MaxGeomSize = 640;
        int gw, gh;
        if (_width >= _height)
        {
            gw = Math.Min((int)_width, MaxGeomSize);
            gh = Math.Max(1, (int)(_height * (float)gw / _width));
        }
        else
        {
            gh = Math.Min((int)_height, MaxGeomSize);
            gw = Math.Max(1, (int)(_width * (float)gh / _height));
        }
        gw = Math.Max(1, gw);
        gh = Math.Max(1, gh);
        float geomScale = (float)gw / _width;

        int rw = Math.Max(1, (int)_width);
        int rh = Math.Max(1, (int)_height);

        var cpuSw = DebugLog.IsEnabled(DebugLogCategory.Present) ? System.Diagnostics.Stopwatch.StartNew() : null;

        var scene = BuildSceneBuffer(baseColor, geomScale);
        var geomPixels = SceneCpuRenderer.RenderToOutput(scene, gw, gh, global::Etch.Gpu.ColorSpace.Srgb);

        if (geomPixels == null || geomPixels.Length == 0)
        {
            return;
        }

        // Upscale the reduced-resolution geometry to native, then rasterize
        // glyphs at native resolution on top so text stays crisp (no blur).
        var pixels = new byte[rw * rh * 4];
        UpscaleRgbaBilinear(geomPixels, gw, gh, pixels, rw, rh);
        _backend.RenderGlyphCommands(pixels, rw, rh, 1.0f);

        // Composite retained layers (e.g. ScrollView content). Without this the
        // CPU path renders only the main scene + main glyphs, so anything captured
        // into a layer never appears (WP-3514).
        if (_activeLayers.Count > 0)
        {
            CompositeLayersCpu(pixels, rw, rh, geomScale, gw, gh);
        }

        if (cpuSw is not null)
        {
            cpuSw.Stop();
            DebugLog.Write(DebugLogCategory.Present,
                $"[{DateTime.Now:O}] CPU fallback: geometry {gw}x{gh} upscaled to {rw}x{rh} + native text in {cpuSw.Elapsed.TotalMilliseconds:F1} ms");
        }

        if (_useGpu && _etchGpuPresenter != null)
        {
            _etchGpuPresenter.PresentCpuFallback(pixels, (uint)rw, (uint)rh);
            if (captureDraws)
            {
                // CPU fallback draws glyphs directly into the pixel buffer —
                // there are no glyph instances, so the snapshot carries shape
                // records only (CPU-fallback fidelity is WP-3514's program).
                PublishDrawSnapshot(captureBaseline);
            }
            return;
        }

        if (_hwnd != IntPtr.Zero)
        {
            // Retain the RGBA frame for CaptureFrame() BEFORE BlitToWindow
            // swaps it to BGRA in place. MarkCapture before NotifyPresented so
            // the captured pixels are attributed to the frame about to present.
            RetainCpuCapture(pixels, rw, rh);
            Cascade.UI.Diagnostics.PresentMonitor.MarkCapture();

            BlitToWindow(_hwnd, pixels, rw, rh, (int)_width, (int)_height);
            Cascade.UI.Diagnostics.PresentMonitor.CpuRenderActive = true;
            Cascade.UI.Diagnostics.PresentMonitor.NotifyPresented();
            if (captureDraws)
            {
                PublishDrawSnapshot(captureBaseline);
            }
        }
    }

    /// <summary>
    /// Copies an RGBA CPU frame into the reused capture buffer (resized only
    /// when the dimensions grow), so CaptureFrame() can serve it without a
    /// per-frame allocation.
    /// </summary>
    /// <summary>
    /// Bilinearly upscales a tightly-packed RGBA8 buffer into a destination of a
    /// different size. Stretches the reduced-resolution CPU geometry pass up to
    /// native resolution before the native-resolution text pass (WP-3514).
    /// </summary>
    private static void UpscaleRgbaBilinear(byte[] src, int sw, int sh, byte[] dst, int dw, int dh)
    {
        if (sw == dw && sh == dh)
        {
            Array.Copy(src, dst, Math.Min(src.Length, dst.Length));
            return;
        }

        float fx = dw > 1 ? (float)(sw - 1) / (dw - 1) : 0f;
        float fy = dh > 1 ? (float)(sh - 1) / (dh - 1) : 0f;
        for (int y = 0; y < dh; y++)
        {
            float syf = y * fy;
            int sy = (int)syf;
            int sy1 = Math.Min(sy + 1, sh - 1);
            float wy = syf - sy;
            int rowOff = sy * sw * 4;
            int row1Off = sy1 * sw * 4;
            int dstRow = y * dw * 4;
            for (int x = 0; x < dw; x++)
            {
                float sxf = x * fx;
                int sx = (int)sxf;
                int sx1 = Math.Min(sx + 1, sw - 1);
                float wx = sxf - sx;
                int i00 = rowOff + sx * 4;
                int i10 = rowOff + sx1 * 4;
                int i01 = row1Off + sx * 4;
                int i11 = row1Off + sx1 * 4;
                int d = dstRow + x * 4;
                for (int c = 0; c < 4; c++)
                {
                    float top = src[i00 + c] * (1 - wx) + src[i10 + c] * wx;
                    float bot = src[i01 + c] * (1 - wx) + src[i11 + c] * wx;
                    dst[d + c] = (byte)(top * (1 - wy) + bot * wy + 0.5f);
                }
            }
        }
    }

    // Reused native-size scratch buffer for compositing each retained layer on
    // the CPU path (avoids a per-layer allocation).
    private byte[]? _layerNativeBuffer;

    /// <summary>
    /// Composites the frame's retained layers (ScrollView content, etc.) onto the
    /// native-resolution CPU framebuffer. Each layer's geometry is rasterized at
    /// the reduced geometry scale and upscaled to match <paramref name="dst"/>,
    /// then alpha-composited at the layer's scroll offset within its viewport
    /// clip; the layer's glyphs are drawn at native resolution on top (WP-3514).
    /// </summary>
    private void CompositeLayersCpu(byte[] dst, int dw, int dh, float geomScale, int gw, int gh)
    {
        int needed = dw * dh * 4;
        foreach (var layer in _activeLayers)
        {
            // Layer scenes are built at geomScale like the main scene — render at
            // the same reduced size then upscale to native to match dst.
            var layerGeom = SceneCpuRenderer.RenderToOutput(layer.Scene, gw, gh, global::Etch.Gpu.ColorSpace.Srgb);
            if (layerGeom == null || layerGeom.Length == 0)
            {
                continue;
            }
            if (_layerNativeBuffer is null || _layerNativeBuffer.Length < needed)
            {
                _layerNativeBuffer = new byte[needed];
            }
            UpscaleRgbaBilinear(layerGeom, gw, gh, _layerNativeBuffer, dw, dh);

            // Layer content (shapes and glyphs) is captured at ABSOLUTE scroll-0
            // positions (BuildLayerSceneBuffer uses the layer's InitialTransform;
            // glyphs were painted at absolute coords). The composite offset is the
            // layer's initial translation plus the scroll, which equals the
            // viewport-clip origin plus the scroll — so the residual shift to
            // apply is just the scroll delta: offset − clipOrigin.
            float effOffX = layer.OffsetX;
            float effOffY = layer.OffsetY;

            int clipX0 = 0, clipY0 = 0, clipX1 = dw, clipY1 = dh;
            if (layer.ViewportClip is Cascade.UI.Rect vc)
            {
                effOffX -= vc.X;
                effOffY -= vc.Y;
                clipX0 = Math.Max(0, (int)MathF.Floor(vc.X));
                clipY0 = Math.Max(0, (int)MathF.Floor(vc.Y));
                clipX1 = Math.Min(dw, (int)MathF.Ceiling(vc.X + vc.Width));
                clipY1 = Math.Min(dh, (int)MathF.Ceiling(vc.Y + vc.Height));
            }

            int ox = (int)MathF.Round(effOffX);
            int oy = (int)MathF.Round(effOffY);
            CompositeOver(dst, dw, dh, _layerNativeBuffer, ox, oy, layer.Opacity, clipX0, clipY0, clipX1, clipY1);

            _backend.RenderGlyphCommandsLayer(dst, dw, dh, layer.GlyphCommands, effOffX, effOffY, layer.ViewportClip);
        }
    }

    /// <summary>
    /// Alpha-composites a native-resolution layer buffer over the destination at
    /// an integer scroll offset, within a clip rect, honoring layer opacity. The
    /// GPU adds the offset to layer positions; sampling the source at
    /// (x − offset) shifts by the same amount.
    /// </summary>
    private static void CompositeOver(byte[] dst, int dw, int dh, byte[] src, int ox, int oy, float opacity,
        int clipX0, int clipY0, int clipX1, int clipY1)
    {
        for (int y = clipY0; y < clipY1; y++)
        {
            int sy = y - oy;
            if (sy < 0 || sy >= dh)
            {
                continue;
            }
            for (int x = clipX0; x < clipX1; x++)
            {
                int sx = x - ox;
                if (sx < 0 || sx >= dw)
                {
                    continue;
                }
                int si = (sy * dw + sx) * 4;
                float sa = src[si + 3] / 255f * opacity;
                if (sa <= 0f)
                {
                    continue;
                }
                int di = (y * dw + x) * 4;
                float ia = 1f - sa;
                dst[di]     = (byte)(src[si]     * sa + dst[di]     * ia + 0.5f);
                dst[di + 1] = (byte)(src[si + 1] * sa + dst[di + 1] * ia + 0.5f);
                dst[di + 2] = (byte)(src[si + 2] * sa + dst[di + 2] * ia + 0.5f);
                dst[di + 3] = (byte)(sa * 255f + dst[di + 3] * ia + 0.5f);
            }
        }
    }

    private void RetainCpuCapture(byte[] rgba, int w, int h)
    {
        int needed = w * h * 4;
        if (_cpuCaptureBuffer is null || _cpuCaptureBuffer.Length < needed)
        {
            _cpuCaptureBuffer = new byte[needed];
        }
        Array.Copy(rgba, _cpuCaptureBuffer, needed);
        _cpuCaptureWidth = w;
        _cpuCaptureHeight = h;
    }

    private SceneBuffer BuildSceneBuffer(ColorValue baseColor, float scale = 1.0f)
    {
        ulong hash = _backend.CommandSequenceHash;
        hash ^= (ulong)EtchBackend.ToArgb(baseColor);
        hash *= 1099511628211;
        hash ^= (ulong)_width;
        hash *= 1099511628211;
        hash ^= (ulong)_height;
        hash *= 1099511628211;
        hash ^= (ulong)BitConverter.SingleToInt32Bits(scale);
        hash *= 1099511628211;

        // A layer recapture emits no main-stream op (only the DrawLayerTexture
        // composite, unchanged), so a recapture that changes only a layer's
        // *internal* content — e.g. a card's selection border inside a ScrollView —
        // hashes identically to the previous frame. Without this, the whole cached
        // scene (with the STALE layer) is reused and the change never appears until
        // an unrelated main-stream edit. A recapture only happens on invalidation
        // (LayerCaptures is cleared each frame and repopulated by PushLayerTexture),
        // so taking the rebuild path whenever one occurred costs nothing on ordinary
        // scroll/idle frames; the per-layer hash below still reuses unchanged layers.
        bool layerRecaptureNeedsRefresh = _backend.LayerCaptures.Count > 0;

        if (_cachedSceneBuffer is not null
            && hash == _cachedSceneHash
            && _cachedBaseColor == baseColor
            && _cachedWidth == _width
            && _cachedHeight == _height
            && _cachedScale == scale
            && !layerRecaptureNeedsRefresh)
        {
            _activeLayers.Clear();
            _activeLayers.AddRange(_cachedActiveLayers);
            return _cachedSceneBuffer;
        }

        _cachedSceneBuffer?.Dispose();
        _activeLayers.Clear();

        // ═══════════════════════════════════════════════════════════════════════════════════
        // Build layer SceneBuffers first — each layer is cached independently
        // by its command hash so scrolling (which changes only the main
        // DrawLayerTexture offset) does not invalidate layer content.
        // ════════════════════════════════════════════════════════════════
        foreach (var (handle, capture) in _backend.LayerCaptures)
        {
            ulong layerHash = ComputeLayerHash(capture);
            if (_cachedLayerScenes.TryGetValue(handle, out var cached) && cached.Hash == layerHash)
            {
                // Layer unchanged — reuse cached SceneBuffer. The hash covers
                // only visual content, so when draw-provenance capture is on,
                // refresh the cached glyph commands anyway: a recapture
                // re-emits them with fresh DebugNodeId tags that the cached
                // (possibly pre-capture) copies lack.
                if (DrawProvenance.CaptureEnabled)
                {
                    _cachedLayerScenes[handle] = (cached.Scene, cached.Hash,
                        capture.GlyphCommands.ToList(), cached.ImageCommands);
                }
                continue;
            }

            // Build new SceneBuffer for this layer
            var layerScene = BuildLayerSceneBuffer(capture, scale);
            var layerInitial = scale == 1.0f
                ? capture.InitialTransform
                : Matrix3x2.CreateScale(scale, scale) * capture.InitialTransform;
            _cachedLayerScenes[handle] = (layerScene, layerHash,
                capture.GlyphCommands.ToList(), ExtractImageCommands(capture.Commands, layerInitial));
        }

        int estimatedCommands = Math.Max(4096, _backend.Commands.Count * 4);
        var sb = SceneBuilder.Begin(estimatedCommands);
        sb.BeginFrame();

        var initialAffine = scale == 1.0f
            ? EGeometry.Affine.Identity
            : new EGeometry.Affine(scale, 0, 0, scale, 0, 0);
        int initialTransformId = sb.AddTransform(initialAffine);
        sb.SetTransform(initialTransformId);

        // Seed the shared op→scene state with the DPI-scale base matrix. SceneBuilder
        // .SetTransform replaces rather than composes, so AppendSceneOp tracks the
        // accumulated matrix itself and adds each product as a single transform.
        var baseMatrix = scale == 1.0f
            ? System.Numerics.Matrix3x2.Identity
            : new System.Numerics.Matrix3x2(scale, 0, 0, scale, 0, 0);
        int identityTransformId = sb.AddTransform(EGeometry.Affine.Identity);
        _appendState.Reset(baseMatrix, initialTransformId, identityTransformId);

        // Fill background with base color
        int bgPaintId = sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(baseColor)));
        sb.FillRect(new EGeometry.Rect(0, 0, _width, _height), bgPaintId, identityTransformId);



        foreach (var cmd in _backend.Commands)
        {
            // The live frame composites retained layers; everything else flows through
            // the shared op→scene dispatch (RENDER-001).
            if (cmd.Kind == EtchBackend.OpKind.DrawLayerTexture)
            {
                ulong handle = (ulong)cmd.W;
                if (_cachedLayerScenes.TryGetValue(handle, out var layerInfo))
                {
                    Cascade.UI.Rect? viewportClip = cmd.HasClipBounds ? cmd.ClipBounds : null;
                    _activeLayers.Add(new LayerRenderInfo(
                        handle, layerInfo.Scene, cmd.X, cmd.Y, cmd.Opacity,
                        layerInfo.GlyphCommands, layerInfo.ImageCommands, viewportClip));
                }
                continue;
            }
            AppendSceneOp(ref sb, cmd, _appendState, "BuildSceneBuffer");
        }

        while (_appendState.ClipStack.Count > 0)
        {
            sb.PopClip();
            _appendState.ClipStack.Pop();
        }
        sb.EndFrame();
        var sceneBuffer = sb.End();

        if (DebugLog.IsEnabled(DebugLogCategory.Transform))
        {
            int pushCount = 0, popCount = 0;
            foreach (var cmd in _backend.Commands)
            {
                if (cmd.Kind == EtchBackend.OpKind.PushTransform)
                {
                    pushCount++;
                }
                if (cmd.Kind == EtchBackend.OpKind.PopTransform)
                {
                    popCount++;
                }
            }
            if (pushCount > 0)
            {
                DebugLog.Write(DebugLogCategory.Transform,
                    $"[{DateTime.Now:O}] BuildSceneBuffer: {pushCount} push, {popCount} pop, {_backend.Commands.Count} total commands");
            }
        }

        _cachedSceneBuffer = sceneBuffer;
        _cachedSceneHash = hash;
        _cachedBaseColor = baseColor;
        _cachedWidth = _width;
        _cachedHeight = _height;
        _cachedScale = scale;
        _cachedActiveLayers.Clear();
        _cachedActiveLayers.AddRange(_activeLayers);

        return sceneBuffer;
    }

    private static ulong ComputeLayerHash(EtchBackend.LayerCapture capture)
    {
        ulong hash = 14695981039346656037;
        const ulong prime = 1099511628211;

        foreach (var cmd in capture.Commands)
        {
            // Use the same comprehensive hashing as ComputeOpHash so that
            // any visual change (color, radius, opacity, stroke, etc.)
            // invalidates the cached layer scene.
            hash ^= EtchBackend.ComputeOpHash(cmd);
            hash *= prime;
        }

        foreach (var glyph in capture.GlyphCommands)
        {
            hash ^= (ulong)BitConverter.SingleToInt32Bits(glyph.FontSize);
            hash *= prime;
            hash ^= (ulong)glyph.Color.GetHashCode();
            hash *= prime;
            hash ^= (ulong)glyph.FontHandle.GetHashCode();
            hash *= prime;
            hash ^= (ulong)glyph.GlyphIds.Length;
            hash *= prime;
            foreach (var id in glyph.GlyphIds)
            {
                hash ^= (ulong)id;
                hash *= prime;
            }
            foreach (var pos in glyph.Positions)
            {
                hash ^= (ulong)BitConverter.SingleToInt32Bits(pos);
                hash *= prime;
            }
        }

        return hash;
    }

    /// <summary>
    /// Collects a layer's <c>DrawImage</c> ops so the presenter can composite them
    /// (the GPU image pass walks the main command stream, which a retained layer's
    /// captured ops never reach). Image ops store raw rects, so this replays the
    /// layer's <c>PushTransform</c>/<c>PopTransform</c> stream — starting from
    /// <paramref name="initialTransform"/>, exactly as <see cref="BuildLayerSceneBuffer"/>
    /// does for shapes — and pairs each image with the accumulated local→device
    /// transform at its capture point. Returns an empty list when the layer draws no
    /// images.
    /// </summary>
    private static List<(EtchBackend.SceneOp Op, Matrix3x2 Transform)> ExtractImageCommands(
        List<EtchBackend.SceneOp> commands, Matrix3x2 initialTransform)
    {
        var images = new List<(EtchBackend.SceneOp, Matrix3x2)>();
        var current = initialTransform;
        var stack = new Stack<Matrix3x2>();
        foreach (var op in commands)
        {
            switch (op.Kind)
            {
                case EtchBackend.OpKind.PushTransform:
                    stack.Push(current);
                    current = op.Matrix * current;
                    break;
                case EtchBackend.OpKind.PopTransform:
                    if (stack.Count > 0)
                    {
                        current = stack.Pop();
                    }
                    break;
                case EtchBackend.OpKind.DrawImage:
                    images.Add((op, current));
                    break;
            }
        }
        return images;
    }

    /// <summary>
    /// Reusable transform/clip state threaded through <see cref="AppendSceneOp"/>, so the
    /// live and layer builders share one op→scene dispatch. <see cref="Reset"/> seeds the
    /// base matrix (DPI scale for the live frame, the layer's InitialTransform for a layer)
    /// at the bottom of the transform stack, where <see cref="AppendSceneOp"/>'s
    /// <c>PopTransform</c> guard keeps it.
    /// </summary>
    private sealed class SceneAppendState
    {
        public readonly Stack<System.Numerics.Matrix3x2> TransformStack = new();
        public readonly Stack<int> ClipStack = new();
        public System.Numerics.Matrix3x2 CurrentMatrix;
        public int CurrentTransformId;
        public int IdentityTransformId;

        public void Reset(System.Numerics.Matrix3x2 baseMatrix, int baseTransformId, int identityTransformId)
        {
            TransformStack.Clear();
            ClipStack.Clear();
            CurrentMatrix = baseMatrix;
            TransformStack.Push(baseMatrix);
            CurrentTransformId = baseTransformId;
            IdentityTransformId = identityTransformId;
        }
    }

    /// <summary>
    /// Appends one captured <see cref="EtchBackend.SceneOp"/> to <paramref name="sb"/>,
    /// mutating <paramref name="st"/>'s transform/clip stacks. RENDER-001: this is the
    /// single op→scene dispatch shared by the live frame (<see cref="BuildSceneBuffer"/>)
    /// and each retained layer (<see cref="BuildLayerSceneBuffer"/>), so a primitive — or
    /// a transform/clip nuance — can never again be handled differently in a layer than in
    /// the main frame. <c>DrawLayerTexture</c> is intentionally absent: the live caller
    /// composites it; for a layer it reaches the fail-loud default (nested layers are
    /// unsupported).
    /// </summary>
    private void AppendSceneOp(ref SceneBuilder sb, EtchBackend.SceneOp cmd, SceneAppendState st, string context)
    {
        int identityTransformId = st.IdentityTransformId;
        switch (cmd.Kind)
        {
            case EtchBackend.OpKind.DrawBackdropBlur:
                // Handled by the presenter's dedicated backdrop-blur pass (it reads
                // these ops straight from backend.Commands), not the SceneBuffer.
                break;

            case EtchBackend.OpKind.DrawRect:
                if (cmd.Fill.HasValue && cmd.Fill.Value.A > 0)
                {
                    int paintId = sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.Fill.Value)));
                    float r = Math.Min(cmd.Radius, Math.Min(cmd.W, cmd.H) * 0.5f);
                    if (r > 0.5f)
                    {
                        var path = EtchBackend.BuildRoundedRectPath(cmd.X, cmd.Y, cmd.W, cmd.H, r);
                        sb.FillPath(sb.AddPath(path), paintId, identityTransformId, FillRule.NonZero);
                    }
                    else
                    {
                        sb.FillRect(new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H), paintId, identityTransformId);
                    }
                }
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    var path = EtchBackend.BuildRoundedRectPath(cmd.X, cmd.Y, cmd.W, cmd.H, Math.Min(cmd.Radius, Math.Min(cmd.W, cmd.H) * 0.5f));
                    int paintId = sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value)));
                    sb.StrokePath(sb.AddPath(path), paintId, identityTransformId, cmd.StrokeWidth, default);
                }
                break;

            case EtchBackend.OpKind.DrawRectGradient:
                if (cmd.GradientStops != null && cmd.GradientStops.Length >= 2)
                {
                    uint id = (uint)sb.AddGradientStops(EtchBackend.ConvertGradientStops(cmd.GradientStops));
                    var paint = cmd.GradientKind == 0 ? Paint.LinearGradient(id) : Paint.RadialGradient(id);
                    int paintId = sb.AddPaint(paint);
                    float r = Math.Min(cmd.Radius, Math.Min(cmd.W, cmd.H) * 0.5f);
                    if (r > 0.5f)
                    {
                        var path = EtchBackend.BuildRoundedRectPath(cmd.X, cmd.Y, cmd.W, cmd.H, r);
                        sb.FillPath(sb.AddPath(path), paintId, identityTransformId, FillRule.NonZero);
                    }
                    else
                    {
                        sb.FillRect(new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H), paintId, identityTransformId);
                    }
                }
                break;

            case EtchBackend.OpKind.DrawPath:
            {
                var path = _backend.GetCompiledPath(cmd.PathHandle);
                if (!path.HasValue)
                {
                    break;
                }
                int pathId = sb.AddPath(path.Value);
                if (cmd.Fill.HasValue && cmd.Fill.Value.A > 0)
                {
                    sb.FillPath(pathId, sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.Fill.Value))), identityTransformId, FillRule.NonZero);
                }
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    sb.StrokePath(pathId, sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value))), identityTransformId, cmd.StrokeWidth, default);
                }
                break;
            }

            case EtchBackend.OpKind.DrawPathGradient:
            {
                var path = _backend.GetCompiledPath(cmd.PathHandle);
                if (!path.HasValue)
                {
                    break;
                }
                int pathId = sb.AddPath(path.Value);
                if (cmd.GradientStops != null && cmd.GradientStops.Length >= 2)
                {
                    uint id = (uint)sb.AddGradientStops(EtchBackend.ConvertGradientStops(cmd.GradientStops));
                    var paint = cmd.GradientKind == 0 ? Paint.LinearGradient(id) : Paint.RadialGradient(id);
                    sb.FillPath(pathId, sb.AddPaint(paint), identityTransformId, FillRule.NonZero);
                }
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    sb.StrokePath(pathId, sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value))), identityTransformId, cmd.StrokeWidth, default);
                }
                break;
            }

            case EtchBackend.OpKind.DrawCircle:
            {
                var path = EtchBackend.BuildCirclePath(cmd.X, cmd.Y, cmd.Radius);
                int pathId = sb.AddPath(path);
                if (cmd.Fill.HasValue && cmd.Fill.Value.A > 0)
                {
                    sb.FillPath(pathId, sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.Fill.Value))), identityTransformId, FillRule.NonZero);
                }
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    sb.StrokePath(pathId, sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value))), identityTransformId, cmd.StrokeWidth, default);
                }
                break;
            }

            case EtchBackend.OpKind.DrawSector:
                if (cmd.Fill.HasValue && cmd.Fill.Value.A > 0)
                {
                    int paintId = sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.Fill.Value)));
                    sb.FillSector(cmd.X, cmd.Y, cmd.Radius, cmd.InnerRadius,
                        cmd.StartRad, cmd.SweepRad, paintId, identityTransformId);
                }
                break;

            case EtchBackend.OpKind.DrawArc:
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    var path = EtchBackend.BuildArcPath(cmd.X, cmd.Y, cmd.Radius, cmd.StartRad, cmd.SweepRad);
                    sb.StrokePath(sb.AddPath(path), sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value))), identityTransformId, cmd.StrokeWidth, default);
                }
                break;

            case EtchBackend.OpKind.DrawLine:
                if (cmd.StrokeColor.HasValue && cmd.StrokeWidth > 0)
                {
                    var path = EtchBackend.BuildLinePath(cmd.X, cmd.Y, cmd.W, cmd.H);
                    sb.StrokePath(sb.AddPath(path), sb.AddPaint(Paint.Solid(EtchBackend.ToArgb(cmd.StrokeColor.Value))), identityTransformId, cmd.StrokeWidth, default);
                }
                break;

            case EtchBackend.OpKind.DrawImage:
            {
                var img = _backend.GetImage(cmd.ImageHandle);
                if (img != null)
                {
                    uint argb = (uint)(((byte)(cmd.Opacity * 255) << 24) | 0xFFFFFF);
                    int paintId = sb.AddPaint(Paint.Solid(argb));
                    sb.DrawImage((int)cmd.ImageHandle, paintId, identityTransformId);
                }
                break;
            }

            case EtchBackend.OpKind.PushTransform:
                st.CurrentMatrix = cmd.Matrix * st.CurrentMatrix;
                st.TransformStack.Push(st.CurrentMatrix);
                st.CurrentTransformId = sb.AddTransform(EtchBackend.ToAffine(st.CurrentMatrix));
                sb.SetTransform(st.CurrentTransformId);
                break;

            case EtchBackend.OpKind.PopTransform:
                // Never pop below the base matrix seeded by Reset (DPI scale / layer
                // InitialTransform). The layer path historically used a `> 0` guard and
                // fell back to Identity on an extra pop — a latent bug this removes.
                if (st.TransformStack.Count > 1)
                {
                    st.TransformStack.Pop();
                    st.CurrentMatrix = st.TransformStack.Peek();
                    st.CurrentTransformId = sb.AddTransform(EtchBackend.ToAffine(st.CurrentMatrix));
                    sb.SetTransform(st.CurrentTransformId);
                }
                break;

            case EtchBackend.OpKind.PushClip:
            {
                var path = EtchBackend.BuildRectPath(cmd.X, cmd.Y, cmd.W, cmd.H);
                sb.PushClip(sb.AddPath(path), FillRule.NonZero);
                st.ClipStack.Push(0);
                break;
            }

            case EtchBackend.OpKind.PushClipRoundedRect:
            {
                var path = EtchBackend.BuildRoundedRectPath(cmd.X, cmd.Y, cmd.W, cmd.H, Math.Min(cmd.Radius, Math.Min(cmd.W, cmd.H) * 0.5f));
                sb.PushClip(sb.AddPath(path), FillRule.NonZero);
                st.ClipStack.Push(0);
                break;
            }

            case EtchBackend.OpKind.PushClipPath:
            {
                var path = _backend.GetCompiledPath(cmd.PathHandle);
                if (path.HasValue)
                {
                    sb.PushClip(sb.AddPath(path.Value), FillRule.NonZero);
                    st.ClipStack.Push(0);
                }
                break;
            }

            case EtchBackend.OpKind.PopClip:
                if (st.ClipStack.Count > 0)
                {
                    sb.PopClip();
                    st.ClipStack.Pop();
                }
                break;

            case EtchBackend.OpKind.PushLayerTexture:
            case EtchBackend.OpKind.PopLayerTexture:
                // Backend-level layer scoping — never emitted into a command stream.
                break;

            default:
                // DrawLayerTexture reaches here only from a layer build (the live caller
                // composites it). A layer compositing another layer (nested ScrollViews)
                // is unsupported — fail loud rather than drop the inner content silently.
                ReportUnhandledOp(cmd.Kind, context);
                break;
        }
    }

    private SceneBuffer BuildLayerSceneBuffer(EtchBackend.LayerCapture capture, float scale)
    {
        if (DebugLog.IsEnabled(DebugLogCategory.Layer))
        {
            DebugLog.Write(DebugLogCategory.Layer,
                $"[{DateTime.Now:O}] Building layer {capture.Handle}: {capture.Commands.Count} commands, {capture.GlyphCommands.Count} glyphs");
        }
        int estimatedCommands = Math.Max(4096, capture.Commands.Count * 4);
        var sb = SceneBuilder.Begin(estimatedCommands);
        sb.BeginFrame();

        var layerBaseMatrix = capture.InitialTransform;
        if (scale != 1.0f)
        {
            layerBaseMatrix = System.Numerics.Matrix3x2.CreateScale(scale, scale) * layerBaseMatrix;
        }
        var initialAffine = EtchBackend.ToAffine(layerBaseMatrix);
        int initialTransformId = sb.AddTransform(initialAffine);
        sb.SetTransform(initialTransformId);

        // Seed the shared op→scene state with the layer's base transform (InitialTransform
        // × DPI scale) and run every captured op through the same dispatch the live frame
        // uses (RENDER-001). DrawLayerTexture has no case there, so a nested layer reaches
        // the fail-loud default rather than dropping its content silently.
        int identityTransformId = sb.AddTransform(EGeometry.Affine.Identity);
        _appendState.Reset(layerBaseMatrix, initialTransformId, identityTransformId);

        foreach (var cmd in capture.Commands)
        {
            AppendSceneOp(ref sb, cmd, _appendState, "BuildLayerSceneBuffer");
        }

        while (_appendState.ClipStack.Count > 0)
        {
            sb.PopClip();
            _appendState.ClipStack.Pop();
        }
        sb.EndFrame();

        if (DebugLog.IsEnabled(DebugLogCategory.Transform))
        {
            int layerPushCount = 0, layerPopCount = 0;
            foreach (var cmd in capture.Commands)
            {
                if (cmd.Kind == EtchBackend.OpKind.PushTransform)
                {
                    layerPushCount++;
                }
                if (cmd.Kind == EtchBackend.OpKind.PopTransform)
                {
                    layerPopCount++;
                }
            }
            if (layerPushCount > 0)
            {
                DebugLog.Write(DebugLogCategory.Transform,
                    $"[{DateTime.Now:O}] BuildLayerSceneBuffer layer {capture.Handle}: {layerPushCount} push, {layerPopCount} pop, {capture.Commands.Count} total commands");
            }
        }

        return sb.End();
    }

    public void EndFrame(ulong frameHandle)
    {
        _backend.Reset();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        NativeMemorySnapshotProvider.Register(null);
        _cachedSceneBuffer?.Dispose();
        _etchGpuPresenter?.Dispose();
        _backend.Dispose();
    }

    private static void BlitToWindow(nint hwnd, byte[] pixels, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        // Swap RGBA → BGRA for StretchDIBits
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte r = pixels[i];
            byte b = pixels[i + 2];
            pixels[i] = b;
            pixels[i + 2] = r;
        }

        var hdc = GetDC(hwnd);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        var bmi = new BITMAPINFO { bmiHeader = new BITMAPINFOHEADER {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = srcWidth, biHeight = -srcHeight, biPlanes = 1, biBitCount = 32, biCompression = 0 }};

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { _ = StretchDIBits(hdc, 0, 0, dstWidth, dstHeight, 0, 0, srcWidth, srcHeight, handle.AddrOfPinnedObject(), ref bmi, 0, 0x00CC0020); }
        finally { handle.Free(); _ = ReleaseDC(hwnd, hdc); }
    }

    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint hdc);
    [DllImport("gdi32.dll")] private static extern int StretchDIBits(nint hdc, int xDst, int yDst, int wDst, int hDst,
        int xSrc, int ySrc, int wSrc, int hSrc, nint bits, ref BITMAPINFO bmi, uint usage, uint rop);

    [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; }
    [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFOHEADER {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }
}

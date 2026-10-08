using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Cascade.UI;
using Cascade.UI.Diagnostics;
using Etch.Scene;
using EGeometry = Etch.Geometry;

namespace Cascade.UI.Backend.Etch;

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


    // The CPU renderer (created on the first CPU frame): renders the recorded frame at native
    // resolution with Etch's CPU composer and presents it through the GPU presenter or GDI.
    private EtchCpuRenderer? _cpu;
    private bool _lastFrameCpu;

    // The frame as recorded for the composers: the main stream plus retained layers (owned by the
    // recorder, replayed at their scroll offsets).
    private readonly EtchRecorder _recorder = new();
    private readonly global::Etch.Compose.DrawRecording _mainRecording = new();
    private readonly Dictionary<ulong, int> _layerCompositeIndex = new();

    public EtchBackendProvider()
    {
        _backend = new EtchBackend();
    }

    /// <summary>The recorder translating backend ops for the composers (tests inspect it).</summary>
    internal EtchRecorder Recorder => _recorder;

    /// <summary>The GPU presenter, when the surface has one (tests inspect it).</summary>
    internal EtchGpuPresenter? GpuPresenter => _etchGpuPresenter;

    /// <summary>The main recording built by the last <see cref="RecordFrame"/>.</summary>
    internal global::Etch.Compose.DrawRecording MainRecording => _mainRecording;

    /// <summary>Records the current command stream (and captured layers) for the composers.</summary>
    internal void RecordFrame(ColorValue baseColor, bool provenance = false)
    {
        _recorder.RecordFrame(_backend, _mainRecording, baseColor, _width, _height, provenance);
    }

    public EtchBackend Backend => _backend;

    /// <summary>Adapter choice passed to the GPU presenter when the surface is created.</summary>
    internal GpuPreference GpuPreference { get; set; } = GpuPreference.Auto;

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
            _etchGpuPresenter = new EtchGpuPresenter(windowHandle, width, height, ResolveGpuPreference(GpuPreference));
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

    // CASCADE_GPU=auto|lowpower|highperformance|software overrides AppConfig.Gpu, so a specific
    // adapter can be exercised (goldens recorded on one GPU, a user report from another) without a
    // rebuild. Unset or unrecognised values keep the configured preference.
    private static GpuPreference ResolveGpuPreference(GpuPreference configured)
    {
        string? value = Environment.GetEnvironmentVariable("CASCADE_GPU")?.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return GpuPreference.Auto;
        }
        if (string.Equals(value, "lowpower", StringComparison.OrdinalIgnoreCase))
        {
            return GpuPreference.LowPower;
        }
        if (string.Equals(value, "highperformance", StringComparison.OrdinalIgnoreCase))
        {
            return GpuPreference.HighPerformance;
        }
        if (string.Equals(value, "software", StringComparison.OrdinalIgnoreCase))
        {
            return GpuPreference.Software;
        }
        return configured;
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
        _cpu?.InvalidatePresentation();
    }

    /// <summary>
    /// The window is hidden: shrink the swapchain and its framebuffer copy to 1×1 so a tray app
    /// does not hold ~35 MB of surface memory all day, and free the image textures, mask pages
    /// and CPU-frame texture (re-created on demand). Nothing is presented while hidden.
    /// </summary>
    public void SuspendSurface()
    {
        _etchGpuPresenter?.Resize(1, 1);
        _etchGpuPresenter?.Trim();
        // The CPU framebuffer (8 MB at 1080p) and its tile buffers go too; the next frame renders in full.
        _cpu?.Release();
    }

    /// <summary>The window is visible again: restore the surface to the window size.</summary>
    public void ResumeSurface()
    {
        if (_width > 0 && _height > 0)
        {
            _etchGpuPresenter?.Resize(_width, _height);
        }
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

    /// <summary>Frames whose present threw (tests: a frame must never fail).</summary>
    internal int PresentErrorCount => presentErrorCount;
    private const int MaxPresentErrorLogEntries = 100;


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
        // A CPU frame is captured from the CPU framebuffer itself: native resolution, exactly
        // the presented pixels, no GPU readback.
        if (_lastFrameCpu && _cpu is not null)
        {
            return _cpu.CaptureFrame();
        }
        return _etchGpuPresenter?.CaptureFrame();
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
    private int snapshotAtlasDimension;

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
                snapshotAtlasDimension,
                snapshotShapes.ToArray(),
                snapshotGlyphs.ToArray(),
                snapshotUncapturedLayers);
        }
    }

    /// <summary>Frees the GPU textures of images destroyed since the last frame.</summary>
    internal void ReleaseDestroyedImages()
    {
        var destroyed = _backend.DestroyedImages;
        if (destroyed.Count == 0)
        {
            return;
        }
        foreach (ulong handle in destroyed)
        {
            _etchGpuPresenter?.ReleaseImage(handle);
        }
        destroyed.Clear();
    }

    // The glyph atlas the last frame drew from: the CPU composer's for a CPU frame.
    private int CurrentAtlasDimension => _lastFrameCpu && _cpu is not null
        ? _cpu.AtlasDimension
        : _etchGpuPresenter?.AtlasDimension ?? 0;

    public AtlasRegionCapture? CaptureAtlasRegion(int u, int v, int width, int height)
    {
        if (_lastFrameCpu && _cpu is not null)
        {
            return _cpu.CaptureAtlasRegion(u, v, width, height);
        }
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
            snapshotAtlasDimension = CurrentAtlasDimension;
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
    /// mirroring the transform/clip semantics the frame is recorded with (<see cref="EtchRecorder"/>):
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
                            // layer's InitialTransform, and the composite offset (cmd.X/Y) is in the
                            // same space, so the residual shift is the offset from the composite's
                            // local origin (cmd.G0/G1) — exactly the shift the recorder replays the
                            // layer at (EtchRecorder). Adding the whole offset double-counted the
                            // origin and dropped layer-canvas shapes ~origin pixels away.
                            float spliceOffX = cmd.X - cmd.G0;
                            float spliceOffY = cmd.Y - cmd.G1;
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
                                    PaintOrder = DrawPaintOrder.LayerCommand(i, record.OpIndex),
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
                            // Layer-local records get their real paint order when spliced.
                            target.Add(new ShapeDrawRecord(
                                pass, kind, minX, minY, maxX, maxY,
                                cmd.Fill, cmd.StrokeColor, cmd.DebugNodeId, i, layerHandle,
                                spliceLayers ? DrawPaintOrder.MainCommand(i) : 0));
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

    // Main-stream command index of each retained layer's composite (draw provenance paint order).
    private void IndexLayerComposites()
    {
        _layerCompositeIndex.Clear();
        var commands = _backend.Commands;
        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i].Kind == EtchBackend.OpKind.DrawLayerTexture)
            {
                _layerCompositeIndex[(ulong)commands[i].W] = i;
            }
        }
    }

    private void PresentFrameCore(ulong frameHandle, ColorValue baseColor)
    {
        bool captureDraws = DrawProvenance.CaptureEnabled;
        long captureBaseline = captureDraws ? PresentMonitor.PresentedFrames : 0;
        if (captureDraws)
        {
            pendingGlyphs.Clear();
        }

        if (DebugLog.IsEnabled(DebugLogCategory.Present))
        {
            DebugLog.Write(DebugLogCategory.Present,
                $"[{DateTime.Now:O}] PresentFrameCore: forceCpu={_forceCpuFallback}, presenter={_etchGpuPresenter != null}, layers={_recorder.LayerCount}");
        }

        ReleaseDestroyedImages();
        if (!_forceCpuFallback && _etchGpuPresenter != null)
        {
            RecordFrame(baseColor, captureDraws);
            var parameters = new global::Etch.Compose.ComposeParameters
            {
                TextGamma = _etchGpuPresenter.TextGamma,
                LightWeight = _etchGpuPresenter.LightWeight,
                Dissolve = _backend.FrameDissolve,
            };
            if (captureDraws)
            {
                IndexLayerComposites();
            }
            _etchGpuPresenter.PresentRecording(_mainRecording, parameters, captureDraws ? pendingGlyphs : null, captureDraws ? _layerCompositeIndex : null);
            _lastFrameCpu = false;
            if (captureDraws)
            {
                PublishDrawSnapshot(captureBaseline);
            }
            return;
        }

        // CPU path (no GPU, CASCADE_FORCE_CPU=1, or render_mode=cpu): the same recording, rendered
        // at native resolution by Etch's CPU composer — the GPU path's draw list, so both agree to
        // within the parity tolerance — re-rendering and presenting only the tiles that changed.
        long cpuStart = System.Diagnostics.Stopwatch.GetTimestamp();
        RecordFrame(baseColor, captureDraws);
        double recordMs = System.Diagnostics.Stopwatch.GetElapsedTime(cpuStart).TotalMilliseconds;
        if (captureDraws)
        {
            IndexLayerComposites();
        }
        var cpuParameters = new global::Etch.Compose.ComposeParameters
        {
            TextGamma = _textGamma,
            LightWeight = _lightWeight,
            Dissolve = _backend.FrameDissolve,
        };
        _cpu ??= new EtchCpuRenderer();
        if (!_lastFrameCpu)
        {
            // GPU frames were presented since the last CPU frame: send the whole CPU frame.
            _cpu.InvalidatePresentation();
        }
        _cpu.Render(_mainRecording, cpuParameters, _width, _height,
            captureDraws ? pendingGlyphs : null, captureDraws ? _layerCompositeIndex : null);
        _lastFrameCpu = true;
        long presentStart = System.Diagnostics.Stopwatch.GetTimestamp();

        if (_useGpu && _etchGpuPresenter != null)
        {
            if (!_etchGpuPresenter.PresentCpuFrame(_cpu.Framebuffer, _cpu.Dirty))
            {
                _cpu.PresentationFailed();
            }
        }
        else if (_hwnd != IntPtr.Zero)
        {
            // MarkCapture before NotifyPresented so the retained frame is attributed to this present.
            Cascade.UI.Diagnostics.PresentMonitor.MarkCapture();
            _ = _cpu.BlitToWindow(_hwnd);
            Cascade.UI.Diagnostics.PresentMonitor.CpuRenderActive = true;
            Cascade.UI.Diagnostics.PresentMonitor.NotifyPresented();
        }
        LastCpuFrameMs = System.Diagnostics.Stopwatch.GetElapsedTime(cpuStart).TotalMilliseconds;
        if (DebugLog.IsEnabled(DebugLogCategory.Present))
        {
            DebugLog.Write(DebugLogCategory.Present, string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"[{DateTime.Now:O}] CPU frame {_width}x{_height}: total {LastCpuFrameMs:F3} ms = record {recordMs:F3} + build {_cpu.LastBuildMs:F3} + render {_cpu.LastRenderMs - _cpu.LastBuildMs:F3} + present {System.Diagnostics.Stopwatch.GetElapsedTime(presentStart).TotalMilliseconds:F3}; {_cpu.LastDamagedPixels} px in {_cpu.Dirty.Length} rects"));
        }
        if (captureDraws)
        {
            PublishDrawSnapshot(captureBaseline);
        }
    }

    /// <summary>Milliseconds the last CPU frame took in the renderer: record, build, composite, present.</summary>
    internal double LastCpuFrameMs { get; private set; }

    /// <summary>
    /// The window needs its client area repainted (WM_PAINT): on the GDI path, send the whole of the
    /// last CPU frame again. The GPU swapchain and DWM keep their own copy, so nothing else does.
    /// </summary>
    public void RepaintWindow()
    {
        if (_lastFrameCpu && _cpu is not null && !(_useGpu && _etchGpuPresenter != null) && _hwnd != IntPtr.Zero)
        {
            _ = _cpu.BlitAll(_hwnd);
        }
    }

    /// <summary>The CPU renderer, once a CPU frame has rendered (tests and diagnostics).</summary>
    internal EtchCpuRenderer? CpuRenderer => _cpu;

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
        _cpu?.Dispose();
        _etchGpuPresenter?.Dispose();
        _backend.Dispose();
    }
}

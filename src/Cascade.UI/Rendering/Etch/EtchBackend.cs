using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Etch.Geometry;
using Etch.Scene;
using Etch.Text.Rasterize;
using Etch.Text.Shape;
using HB = Etch.Text.HarfBuzz;

namespace Cascade.UI.Backend.Etch;

internal sealed class EtchBackend : IDisposable
{
    private readonly Dictionary<ulong, BezPath> _compiledPaths = new();
    private ulong _nextPathHandle = 1;
    private bool _disposed;

    internal readonly List<SceneOp> Commands = new();
    internal readonly List<GlyphOp> GlyphCommands = new();
    internal uint Width, Height;

    /// <summary>
    /// WP-3526: text-weight gamma for the CPU glyph blitter, kept in lock-step with
    /// the GPU presenter's <see cref="EtchGpuPresenter.TextGamma"/> by the provider
    /// so a forced-CPU machine renders text at the same weight as a GPU one.
    /// </summary>
    internal float TextGamma { get; set; } = EtchGpuPresenter.DefaultTextGamma;

    /// <summary>
    /// WP-3537: adaptive light-weight strength for the CPU glyph blitter (1 = full
    /// contrast-adaptive, 0 = legacy symmetric), kept in lock-step with
    /// <see cref="EtchGpuPresenter.LightWeight"/> by the provider.
    /// </summary>
    internal float LightWeight { get; set; } = EtchGpuPresenter.DefaultLightWeight;

    private Matrix3x2 _currentTransform = Matrix3x2.Identity;
    private readonly Stack<Matrix3x2> _transformStack = new();

    // Layer opacity — PushLayer(opacity) multiplies this; every emitted shape,
    // glyph and image scales its colour alpha by the current product so a
    // PushOpacity scope actually fades its whole subtree (there is no
    // render-to-texture layer compositor on this path). Simple ambient-alpha:
    // overlapping elements inside one scope double-blend, which is imperceptible
    // for page transitions and control fades.
    private float _currentOpacity = 1f;
    private readonly Stack<float> _opacityStack = new();

    // Per-frame pixel-dissolve threshold [0,1], set by the painter for a dissolve
    // page transition and read by the presenter into the surface uniform. Each
    // fragment whose screen-space hash noise is below this threshold is discarded,
    // so raising it scatters the frame away pixel by pixel. 0 = disabled (no-op).
    internal float FrameDissolve { get; set; }

    // Clip stack — tracks current clip bounds for glyph clipping
    private readonly Stack<Rect> _clipStack = new();
    private Rect _currentClipBounds;

    // Overlay capture — popup text rendered on top of main frame text
    private readonly Stack<object?> _overlayStack = new();
    private readonly Stack<Rect> _overlayBoundsStack = new();
    internal readonly List<GlyphOp> OverlayGlyphCommands = new();
    internal readonly List<Rect> OverlayBounds = new();
    private bool IsCapturingOverlay => _overlayStack.Count > 0;

    // Command-stream index at which deferred-overlay (popup) painting begins. DrawImage ops BEFORE
    // this are main-frame content — culled where an OverlayBounds rect covers them, so a row icon
    // behind an open dropdown doesn't bleed through it (glyphs are already culled the same way). Ops
    // at/after this belong to the overlays themselves and are never culled. int.MaxValue = no overlay.
    internal int OverlayCommandStart = int.MaxValue;

    /// <summary>Records that deferred-overlay painting is about to begin (see OverlayCommandStart).</summary>
    internal void MarkOverlayStart() => OverlayCommandStart = Commands.Count;

    // Layer texture compositing — Flutter-style retained layers
    private ulong _nextLayerHandle = 1;
    private ulong? _activeLayerHandle;
    internal readonly Dictionary<ulong, LayerCapture> LayerCaptures = new();

    internal sealed class LayerCapture
    {
        public ulong Handle;
        public float Width, Height;
        public System.Numerics.Matrix3x2 InitialTransform;
        public readonly List<SceneOp> Commands = new();
        public readonly List<GlyphOp> GlyphCommands = new();
    }

    // Incremental hash of all scene commands — used by EtchBackendProvider to
    // skip rebuilding the SceneBuffer when the command sequence is unchanged.
    internal ulong CommandSequenceHash { get; private set; } = FnvOffsetBasis;
    private const ulong FnvOffsetBasis = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    private static void MixHash(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= FnvPrime;
    }

    private static void MixHash(ref ulong hash, float value)
    {
        hash ^= (ulong)BitConverter.SingleToInt32Bits(value);
        hash *= FnvPrime;
    }

    private static void MixHash(ref ulong hash, int value)
    {
        hash ^= (ulong)value;
        hash *= FnvPrime;
    }

    internal static ulong ComputeOpHash(SceneOp op)
    {
        ulong hash = FnvOffsetBasis;
        MixHash(ref hash, (ulong)op.Kind);
        MixHash(ref hash, op.X);
        MixHash(ref hash, op.Y);
        MixHash(ref hash, op.W);
        MixHash(ref hash, op.H);
        MixHash(ref hash, op.Radius);
        MixHash(ref hash, op.StartRad);
        MixHash(ref hash, op.SweepRad);
        MixHash(ref hash, op.StrokeWidth);
        MixHash(ref hash, op.G0);
        MixHash(ref hash, op.G1);
        MixHash(ref hash, op.G2);
        MixHash(ref hash, op.G3);
        MixHash(ref hash, op.GradientKind);
        MixHash(ref hash, op.PathHandle);
        MixHash(ref hash, op.ImageHandle);
        MixHash(ref hash, op.Opacity);
        MixHash(ref hash, op.Matrix.M11);
        MixHash(ref hash, op.Matrix.M12);
        MixHash(ref hash, op.Matrix.M21);
        MixHash(ref hash, op.Matrix.M22);
        MixHash(ref hash, op.Matrix.M31);
        MixHash(ref hash, op.Matrix.M32);
        MixHash(ref hash, op.Fill.HasValue ? ToArgb(op.Fill.Value) : 0u);
        MixHash(ref hash, op.StrokeColor.HasValue ? ToArgb(op.StrokeColor.Value) : 0u);
        if (op.GradientStops != null)
        {
            MixHash(ref hash, op.GradientStops.Length);
            for (int i = 0; i < op.GradientStops.Length; i++)
            {
                MixHash(ref hash, op.GradientStops[i].Offset);
                MixHash(ref hash, ToArgb(op.GradientStops[i].Color));
            }
        }
        else
        {
            MixHash(ref hash, 0);
        }
        return hash;
    }

    // Current draw-provenance tag — set by the paint pass via
    // SetDrawProvenance, stamped onto every op emitted while active.
    private string? currentDebugNodeId;

    /// <summary>
    /// Tags subsequent ops with the emitting node's DevTools id. Called only
    /// while DrawProvenance.CaptureEnabled — costs one string-reference store
    /// per op otherwise (currentDebugNodeId stays null).
    /// </summary>
    public void SetDrawProvenance(string? nodeId)
    {
        currentDebugNodeId = nodeId;
    }

    private void AddCommand(SceneOp op)
    {
        op.DebugNodeId = currentDebugNodeId;

        if (_currentOpacity < 1f)
        {
            ApplyLayerOpacity(op);
        }

        if (IsCapturingOverlay)
        {
            ExpandOverlayBounds(op);
        }

        if (_activeLayerHandle.HasValue)
        {
            if (LayerCaptures.TryGetValue(_activeLayerHandle.Value, out var layer))
            {
                layer.Commands.Add(op);
            }
        }
        else
        {
            Commands.Add(op);
            CommandSequenceHash = MixHash(CommandSequenceHash, ComputeOpHash(op));
        }
    }

    // Scales a command's colour alpha by the current layer opacity. Mutates the
    // (freshly-allocated) op in place; runs before the op is hashed so a changing
    // opacity invalidates the scene cache.
    private void ApplyLayerOpacity(SceneOp op)
    {
        if (op.Fill is ColorValue fill)
        {
            op.Fill = fill.ScaleAlpha(_currentOpacity);
        }

        if (op.StrokeColor is ColorValue stroke)
        {
            op.StrokeColor = stroke.ScaleAlpha(_currentOpacity);
        }

        if (op.Kind == OpKind.DrawImage)
        {
            op.Opacity *= _currentOpacity;
        }

        if (op.GradientStops is { Length: > 0 } stops)
        {
            var scaled = new GradientStop[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                scaled[i] = new GradientStop(stops[i].Offset, stops[i].Color.ScaleAlpha(_currentOpacity));
            }
            op.GradientStops = scaled;
        }
    }

    private void ExpandOverlayBounds(SceneOp op)
    {
        if (_overlayBoundsStack.Count == 0)
        {
            return;
        }

        // Skip non-drawing commands: clips, transforms, layer ops.
        // Skip DrawPath / DrawPathGradient — path bounds are not available
        // at this point (only the compiled handle is stored). The enclosing
        // rects (checkbox bg, dropdown bg) already cover the area.
        if (op.Kind == OpKind.PushClip || op.Kind == OpKind.PopClip ||
            op.Kind == OpKind.PushClipPath || op.Kind == OpKind.PushClipRoundedRect ||
            op.Kind == OpKind.PushTransform || op.Kind == OpKind.PopTransform ||
            op.Kind == OpKind.DrawLayerTexture ||
            op.Kind == OpKind.DrawPath || op.Kind == OpKind.DrawPathGradient)
        {
            return;
        }

        float opMinX = op.X;
        float opMinY = op.Y;
        float opMaxX = op.X + op.W;
        float opMaxY = op.Y + op.H;

        // DrawLine stores end point in W/H, not width/height.
        if (op.Kind == OpKind.DrawLine)
        {
            opMinX = Math.Min(op.X, op.W);
            opMinY = Math.Min(op.Y, op.H);
            opMaxX = Math.Max(op.X, op.W);
            opMaxY = Math.Max(op.Y, op.H);
        }

        // Arcs and circles are centered; expand to bounding box
        if (op.Kind == OpKind.DrawArc || op.Kind == OpKind.DrawCircle)
        {
            opMinX = op.X - op.Radius;
            opMinY = op.Y - op.Radius;
            opMaxX = op.X + op.Radius;
            opMaxY = op.Y + op.Radius;
        }

        // Geometry commands are stored in logical coordinates, but glyph
        // positions are transformed by _currentTransform (DPI scale).
        // Transform geometry bounds to physical coordinates so they match.
        if (_currentTransform != Matrix3x2.Identity)
        {
            var tl = Vector2.Transform(new Vector2(opMinX, opMinY), _currentTransform);
            var tr = Vector2.Transform(new Vector2(opMaxX, opMinY), _currentTransform);
            var bl = Vector2.Transform(new Vector2(opMinX, opMaxY), _currentTransform);
            var br = Vector2.Transform(new Vector2(opMaxX, opMaxY), _currentTransform);
            opMinX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
            opMinY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
            opMaxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
            opMaxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
        }

        var current = _overlayBoundsStack.Pop();
        float minX, minY, maxX, maxY;

        // Empty state sentinel: first command in this overlay scope
        if (current.X == float.MaxValue && current.Y == float.MaxValue &&
            current.Width == 0 && current.Height == 0)
        {
            minX = opMinX;
            minY = opMinY;
            maxX = opMaxX;
            maxY = opMaxY;
        }
        else
        {
            minX = current.X;
            minY = current.Y;
            maxX = current.X + current.Width;
            maxY = current.Y + current.Height;
        }

        if (opMinX < minX) { minX = opMinX; }
        if (opMinY < minY) { minY = opMinY; }
        if (opMaxX > maxX) { maxX = opMaxX; }
        if (opMaxY > maxY) { maxY = opMaxY; }

        _overlayBoundsStack.Push(new Rect(minX, minY, maxX - minX, maxY - minY));
    }



    private void AddGlyphCommand(GlyphOp op)
    {
        op.DebugNodeId = currentDebugNodeId;
        if (IsCapturingOverlay)
        {
            OverlayGlyphCommands.Add(op);
        }
        else if (_activeLayerHandle.HasValue)
        {
            if (LayerCaptures.TryGetValue(_activeLayerHandle.Value, out var layer))
            {
                layer.GlyphCommands.Add(op);
            }
        }
        else
        {
            GlyphCommands.Add(op);
            // Fold glyph colour into the scene hash so a text-only opacity change
            // (a fading page with no shapes) still invalidates the cached scene.
            CommandSequenceHash = MixHash(CommandSequenceHash, (ulong)(uint)op.Color.GetHashCode());
        }
    }

    private static ulong MixHash(ulong hash, ulong value)
    {
        hash ^= value;
        hash *= FnvPrime;
        return hash;
    }

    // Font cache
    private readonly Dictionary<ulong, FontEntry> _fonts = new();
    private ulong _nextFontHandle = 1;

    // Image cache
    private readonly Dictionary<ulong, ImageEntry> _images = new();
    private ulong _nextImageHandle = 1;

    internal sealed class ImageEntry
    {
        public byte[] Pixels;
        public int Width;
        public int Height;

        public ImageEntry(byte[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }
    }

    private sealed class FontEntry
    {
        public byte[] Data;
        public int UnitsPerEm;
        public readonly Dictionary<float, FontFace> Faces = new();

        public FontEntry(byte[] data, int unitsPerEm)
        {
            Data = data;
            UnitsPerEm = unitsPerEm;
        }
    }

    internal void Reset()
    {
        currentDebugNodeId = null;
        Commands.Clear();
        GlyphCommands.Clear();
        OverlayGlyphCommands.Clear();
        OverlayBounds.Clear();
        OverlayCommandStart = int.MaxValue;
        _overlayStack.Clear();
        _overlayBoundsStack.Clear();
        _clipStack.Clear();
        _currentClipBounds = default;
        _compiledPaths.Clear();
        _currentTransform = Matrix3x2.Identity;
        _transformStack.Clear();
        _currentOpacity = 1f;
        _opacityStack.Clear();
        FrameDissolve = 0f;
        CommandSequenceHash = FnvOffsetBasis;
        _activeLayerHandle = null;
        LayerCaptures.Clear();
        // NOTE: _nextLayerHandle is intentionally NOT reset here — handles must stay
        // globally unique across frames so a ScrollView's retained-layer cache key is
        // stable. See NextLayerHandle().
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Reset();
        foreach (var entry in _fonts.Values)
        {
            foreach (var face in entry.Faces.Values)
            {
                face.Dispose();
            }
            entry.Faces.Clear();
        }
        _fonts.Clear();
        _images.Clear();
    }

    internal ImageEntry? GetImage(ulong handle)
    {
        _images.TryGetValue(handle, out var entry);
        return entry;
    }

    // ════════════════════════════════════════════════════════════════
    // Render commands — append to the command buffer
    // ════════════════════════════════════════════════════════════════

    public void DrawRect(ulong frame, float x, float y, float w, float h, float radius,
        ColorValue? fill, ColorValue? strokeColor, float strokeWidth)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawRect, X = x, Y = y, W = w, H = h, Radius = radius,
            Fill = fill, StrokeColor = strokeColor, StrokeWidth = strokeWidth });
    }

    public void DrawRectGradient(ulong frame, float x, float y, float w, float h, float radius,
        int gradientKind, ReadOnlySpan<GradientStop> stops, float p0, float p1, float p2, float p3)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawRectGradient, X = x, Y = y, W = w, H = h, Radius = radius,
            GradientKind = gradientKind, G0 = p0, G1 = p1, G2 = p2, G3 = p3, GradientStops = stops.ToArray() });
    }

    public void PushTransform(ulong frame, Matrix3x2 matrix)
    {
        _transformStack.Push(_currentTransform);
        _currentTransform = matrix * _currentTransform;
        AddCommand(new SceneOp { Kind = OpKind.PushTransform, Matrix = matrix });
    }

    public void PopTransform(ulong frame)
    {
        if (_transformStack.Count > 0)
        {
            _currentTransform = _transformStack.Pop();
        }
        AddCommand(new SceneOp { Kind = OpKind.PopTransform });
    }

    public void PushClip(ulong frame, float x, float y, float w, float h)
    {
        AddCommand(new SceneOp { Kind = OpKind.PushClip, X = x, Y = y, W = w, H = h });
        // Transform clip rect to absolute coordinates for glyph culling.
        // Glyph positions in DrawGlyphs are transformed by _currentTransform,
        // so clip bounds must also be in absolute/screen coordinates.
        var tl = Vector2.Transform(new Vector2(x, y), _currentTransform);
        var br = Vector2.Transform(new Vector2(x + w, y + h), _currentTransform);
        var tr = Vector2.Transform(new Vector2(x + w, y), _currentTransform);
        var bl = Vector2.Transform(new Vector2(x, y + h), _currentTransform);
        float minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
        float minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
        float maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
        float maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
        var clipRect = new Rect(minX, minY, maxX - minX, maxY - minY);
        _clipStack.Push(clipRect);
        _currentClipBounds = _clipStack.Count == 1
            ? clipRect
            : IntersectRects(_currentClipBounds, clipRect);
    }

    public void PushClipPath(ulong frame, ulong path)
    {
        AddCommand(new SceneOp { Kind = OpKind.PushClipPath, PathHandle = path });
    }

    public void PushClipRoundedRect(ulong frame, float x, float y, float w, float h, float radius)
    {
        AddCommand(new SceneOp { Kind = OpKind.PushClipRoundedRect, X = x, Y = y, W = w, H = h, Radius = radius });
        var tl = Vector2.Transform(new Vector2(x, y), _currentTransform);
        var br = Vector2.Transform(new Vector2(x + w, y + h), _currentTransform);
        var tr = Vector2.Transform(new Vector2(x + w, y), _currentTransform);
        var bl = Vector2.Transform(new Vector2(x, y + h), _currentTransform);
        float minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
        float minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
        float maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
        float maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
        var clipRect = new Rect(minX, minY, maxX - minX, maxY - minY);
        _clipStack.Push(clipRect);
        _currentClipBounds = _clipStack.Count == 1
            ? clipRect
            : IntersectRects(_currentClipBounds, clipRect);
    }

    public void PopClip(ulong frame)
    {
        AddCommand(new SceneOp { Kind = OpKind.PopClip });
        if (_clipStack.Count > 0)
        {
            _clipStack.Pop();
            RecomputeClipBounds();
        }
    }

    private void RecomputeClipBounds()
    {
        if (_clipStack.Count == 0)
        {
            _currentClipBounds = default;
        }
        else
        {
            var result = _clipStack.Peek();
            foreach (var r in _clipStack)
            {
                result = IntersectRects(result, r);
            }
            _currentClipBounds = result;
        }
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        float x = Math.Max(a.X, b.X);
        float y = Math.Max(a.Y, b.Y);
        float right = Math.Min(a.X + a.Width, b.X + b.Width);
        float bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= x || bottom <= y)
        {
            return default;
        }
        return new Rect(x, y, right - x, bottom - y);
    }

    public ulong CompilePath(ReadOnlySpan<byte> commands, ReadOnlySpan<float> data)
    {
        var path = ConvertCascadePath(commands, data);
        var handle = _nextPathHandle++;
        _compiledPaths[handle] = path;
        return handle;
    }

    public void DrawPath(ulong frame, ulong pathHandle,
        ColorValue? fill, ColorValue? strokeColor, float strokeWidth, StrokeCap cap, StrokeJoin join)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawPath, PathHandle = pathHandle,
            Fill = fill, StrokeColor = strokeColor, StrokeWidth = strokeWidth });
    }

    // No-op stub (path handles are retained for the frame); instance method to match
    // the call surface in DrawContext. Suppress the "could be static" suggestion.
#pragma warning disable CA1822
    public void DestroyPath(ulong path) { }
#pragma warning restore CA1822

    public void DrawCircle(ulong frame, float cx, float cy, float radius,
        ColorValue? fill, ColorValue? strokeColor, float strokeWidth, StrokeCap cap, StrokeJoin join)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawCircle, X = cx, Y = cy, Radius = radius,
            Fill = fill, StrokeColor = strokeColor, StrokeWidth = strokeWidth });
    }

    public void DrawSector(ulong frame, float cx, float cy, float outerRadius,
        float innerRadius, float startRad, float sweepRad, ColorValue fill)
    {
        AddCommand(new SceneOp
        {
            Kind = OpKind.DrawSector,
            X = cx,
            Y = cy,
            Radius = outerRadius,
            InnerRadius = innerRadius,
            StartRad = startRad,
            SweepRad = sweepRad,
            Fill = fill
        });
    }

    public void DrawArc(ulong frame, float cx, float cy, float radius,
        float startRad, float sweepRad, ColorValue sc, float sw, StrokeCap cap, StrokeJoin join)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawArc, X = cx, Y = cy, Radius = radius,
            StartRad = startRad, SweepRad = sweepRad, StrokeColor = sc, StrokeWidth = sw });
    }

    public void DrawLine(ulong frame, float x1, float y1, float x2, float y2,
        ColorValue sc, float sw, StrokeCap cap, StrokeJoin join)
    {
        AddCommand(new SceneOp { Kind = OpKind.DrawLine, X = x1, Y = y1, W = x2, H = y2, StrokeColor = sc, StrokeWidth = sw });
    }

    public void DrawImage(ulong frame, ulong image, float dx, float dy, float dw, float dh, float o)
    {
        AddCommand(new SceneOp
        {
            Kind = OpKind.DrawImage,
            ImageHandle = image,
            X = dx,
            Y = dy,
            W = dw,
            H = dh,
            Opacity = o,
        });
    }

    public ulong UploadImage(ReadOnlySpan<byte> pixels, int width, int height)
    {
        byte[] copy = pixels.ToArray();
        var handle = _nextImageHandle++;
        _images[handle] = new ImageEntry(copy, width, height);
        return handle;
    }

    public void DestroyImage(ulong image)
    {
        _images.Remove(image);
    }
    public void DrawPathGradient(ulong frame, ulong path, int gk, ReadOnlySpan<GradientStop> s, float p0, float p1, float p2, float p3, ColorValue? sc, float sw, StrokeCap c, StrokeJoin j)
    {
        AddCommand(new SceneOp
        {
            Kind = OpKind.DrawPathGradient,
            PathHandle = path,
            GradientKind = gk,
            GradientStops = s.ToArray(),
            G0 = p0, G1 = p1, G2 = p2, G3 = p3,
            StrokeColor = sc, StrokeWidth = sw
        });
    }
    // PushLayer applies an opacity to everything drawn until the matching PopLayer,
    // by scaling emitted colour alpha (see _currentOpacity). Blend modes other than
    // Normal still require the layer-scene capture path and are not handled here.
    public void PushLayer(ulong frame, float opacity, BlendMode blend)
    {
        _opacityStack.Push(_currentOpacity);
        _currentOpacity *= Math.Clamp(opacity, 0f, 1f);
    }

    public void PopLayer(ulong frame)
    {
        if (_opacityStack.Count > 0)
        {
            _currentOpacity = _opacityStack.Pop();
        }
    }

    // No-op: blurred-shadow compositing happens through EtchBackendProvider's
    // layer-scene capture path, not this per-op backend call. Instance method to
    // match the call surface in DrawContext, so suppress the "could be static" hint.
    // (Drop-shadow blur is done in DrawContext.DrawBlurredRoundedRect via layered
    // rounded rects, not here.)
#pragma warning disable CA1822
    public void DrawBlurredRoundedRect(ulong frame, float x, float y, float w, float h, float r, float sd, ColorValue color) { }
#pragma warning restore CA1822

    /// <summary>
    /// Frosted-glass backdrop blur: records a rounded rect that the presenter fills
    /// with a Gaussian blur of the framebuffer behind it, tinted by <paramref name="tint"/>.
    /// Corners/radius/sigma are baked to device space here (like glyphs) so the
    /// presenter's blur pass can read them directly.
    /// </summary>
    public void DrawBackdropBlur(ulong frame, float x, float y, float w, float h, float radius, float sigma, ColorValue tint)
    {
        var tl = Vector2.Transform(new Vector2(x, y), _currentTransform);
        var br = Vector2.Transform(new Vector2(x + w, y + h), _currentTransform);
        float scale = MathF.Max(0.0001f, (_currentTransform.M11 + _currentTransform.M22) * 0.5f);

        AddCommand(new SceneOp
        {
            Kind = OpKind.DrawBackdropBlur,
            X = tl.X,
            Y = tl.Y,
            W = br.X - tl.X,
            H = br.Y - tl.Y,
            Radius = radius * scale,
            StrokeWidth = sigma * scale,
            Fill = tint,
        });
    }

    // ════════════════════════════════════════════════════════════════
    // Font & text
    // ════════════════════════════════════════════════════════════════

    public ulong LoadFont(ReadOnlySpan<byte> fontData, int faceIndex)
    {
        byte[] copy = fontData.ToArray();
        int unitsPerEm = ReadUnitsPerEm(copy);
        var handle = _nextFontHandle++;
        _fonts[handle] = new FontEntry(copy, unitsPerEm);
        return handle;
    }

    public void DestroyFont(ulong fontHandle)
    {
        if (_fonts.Remove(fontHandle, out var entry))
        {
            foreach (var face in entry.Faces.Values)
            {
                face.Dispose();
            }
        }
    }

    public void DrawGlyphs(ulong frame, ulong fontHandle, ReadOnlySpan<ushort> glyphIds, ReadOnlySpan<float> positions, float fontSize, ColorValue color)
    {
        if (glyphIds.Length == 0 || fontSize <= 0)
        {
            return;
        }
        var ids = new ushort[glyphIds.Length];
        glyphIds.CopyTo(ids);
        var pos = new float[positions.Length];
        for (int i = 0; i < positions.Length / 2; i++)
        {
            var t = Vector2.Transform(new Vector2(positions[i * 2], positions[i * 2 + 1]), _currentTransform);
            pos[i * 2] = t.X;
            pos[i * 2 + 1] = t.Y;
        }

        // Fade text with the enclosing PushOpacity scope (glyphs render in their
        // own final pass, so this is the only way opacity reaches them).
        if (_currentOpacity < 1f)
        {
            color = color.ScaleAlpha(_currentOpacity);
        }

        AddGlyphCommand(new GlyphOp
        {
            GlyphIds = ids,
            Positions = pos,
            FontSize = fontSize,
            FontHandle = fontHandle,
            Color = color,
            ScaleX = _currentTransform.M11,
            ScaleY = _currentTransform.M22,
            ClipBounds = _currentClipBounds,
            HasClipBounds = _clipStack.Count > 0,
        });
    }

    // ════════════════════════════════════════════════════════════════
    // Overlay capture — popup text rendered on top of main frame
    // ════════════════════════════════════════════════════════════════

    public void PushOverlay(ulong frame)
    {
        _overlayStack.Push(null);
        _overlayBoundsStack.Push(new Rect(float.MaxValue, float.MaxValue, 0, 0));
    }

    public void PopOverlay(ulong frame)
    {
        if (_overlayStack.Count > 0)
        {
            _overlayStack.Pop();
            var bounds = _overlayBoundsStack.Pop();
            if (bounds.X < float.MaxValue && bounds.Width > 0 && bounds.Height > 0)
            {
                OverlayBounds.Add(bounds);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Layer texture compositing — Flutter-style retained layers
    // ════════════════════════════════════════════════════════════════

    // The most recently created layer handle — used by NodePainter to retrieve
    // the handle after a PushLayerTexture/PopLayerTexture scope.
    internal ulong? LastCreatedLayerHandle { get; private set; }

    /// <summary>
    /// Allocates a globally-unique, stable layer handle. The counter is monotonic and
    /// is NOT reset per frame, so a handle assigned to a ScrollView's retained layer
    /// stays unique for that ScrollView's lifetime. (Resetting it per frame and handing
    /// handles out in recapture order let two ScrollViews collide on the same handle
    /// when the recapture set changed between frames — e.g. a control animating made the
    /// content layer steal the sidebar's handle, blanking the nav.)
    /// </summary>
    internal ulong NextLayerHandle() => _nextLayerHandle++;

    // Ambient clip saved across a layer capture. A retained layer captures its FULL
    // content into its own texture space and is clipped to the viewport later, at
    // composite time (DrawLayerTexture.ClipBounds). The clip that happens to be active
    // when capture starts (e.g. a SplitView pane clip around the ScrollView) must NOT
    // apply during capture — glyphs bake _currentClipBounds at capture time, so an
    // ambient clip would cut off every glyph below the viewport in the texture while
    // the geometry (driven by in-stream clip ops) stays unclipped. Reset here, restore
    // on pop. A stack handles nested captures. No-op when nothing is clipped.
    private readonly Stack<(Rect[] Clips, Rect Bounds)> _layerClipSaves = new();

    public ulong PushLayerTexture(ulong frame, ulong handle, float width, float height)
    {
        _activeLayerHandle = handle;
        LastCreatedLayerHandle = handle;
        LayerCaptures[handle] = new LayerCapture
        {
            Handle = handle,
            Width = width,
            Height = height,
            InitialTransform = _currentTransform,
        };

        _layerClipSaves.Push((_clipStack.ToArray(), _currentClipBounds));
        _clipStack.Clear();
        _currentClipBounds = default;
        return handle;
    }

    public void PopLayerTexture(ulong frame, ulong layerHandle)
    {
        _activeLayerHandle = null;

        if (_layerClipSaves.Count > 0)
        {
            var (clips, bounds) = _layerClipSaves.Pop();
            _clipStack.Clear();
            // ToArray() yields top-first; push bottom-first to rebuild the same order.
            for (int i = clips.Length - 1; i >= 0; i--)
            {
                _clipStack.Push(clips[i]);
            }
            _currentClipBounds = bounds;
        }
    }

    public ulong? TryGetLastLayerHandle() => LastCreatedLayerHandle;

    public void DrawLayerTexture(ulong frame, ulong layerHandle, float x, float y, float opacity)
    {
        var t = Vector2.Transform(new Vector2(x, y), _currentTransform);
        AddCommand(new SceneOp
        {
            Kind = OpKind.DrawLayerTexture,
            X = t.X,
            Y = t.Y,
            W = layerHandle,
            Opacity = opacity,
            // Capture the active (screen-space) clip so the presenter can clip
            // the composited layer to its viewport — _currentClipBounds is
            // already transformed to absolute coords, matching the offset above.
            ClipBounds = _currentClipBounds,
            HasClipBounds = _clipStack.Count > 0,
        });
    }

    // ════════════════════════════════════════════════════════════════
    // Glyph rendering onto RGBA pixel buffer (CPU fallback path)
    // ════════════════════════════════════════════════════════════════

    public void RenderGlyphCommands(Span<byte> rgbaPixels, int width, int height, float scale = 1.0f)
    {
        foreach (var op in GlyphCommands)
        {
            RenderGlyphOp(op, rgbaPixels, width, height, scale);
        }
    }

    /// <summary>
    /// Renders a retained layer's captured glyphs onto the CPU framebuffer at the
    /// composite offset, clipped to the layer's viewport (WP-3514). Positions are
    /// in the layer's content space (captured at scroll 0); the offset applies the
    /// scroll and the viewport clip keeps content from bleeding outside it.
    /// </summary>
    public void RenderGlyphCommandsLayer(Span<byte> rgbaPixels, int width, int height,
        IReadOnlyList<GlyphOp> glyphs, float offsetX, float offsetY, Rect? viewportClip)
    {
        foreach (var op in glyphs)
        {
            RenderGlyphOp(op, rgbaPixels, width, height, 1.0f, offsetX, offsetY, viewportClip);
        }
    }

    private void RenderGlyphOp(GlyphOp op, Span<byte> pixels, int width, int height, float scale,
        float offsetX = 0f, float offsetY = 0f, Rect? viewportClip = null)
    {
        var face = GetOrCreateFontFace(op.FontHandle, op.FontSize * scale);
        if (face == null)
        {
            return;
        }

        var colorBytes = ExtractSrgbBytes(op.Color);
        byte textR = colorBytes.R;
        byte textG = colorBytes.G;
        byte textB = colorBytes.B;
        byte textA = colorBytes.A;

        // Effective clip = (the op's own content clip, shifted by the composite
        // offset) intersected with the layer viewport clip (screen-space). Stays
        // unclipped for the ordinary main-frame path (no offset, no viewport).
        float clipX = 0, clipY = 0, clipW = float.MaxValue, clipH = float.MaxValue;
        if (op.HasClipBounds || viewportClip.HasValue)
        {
            float l = float.MinValue, t = float.MinValue, r = float.MaxValue, b = float.MaxValue;
            if (op.HasClipBounds)
            {
                l = op.ClipBounds.X * scale + offsetX;
                t = op.ClipBounds.Y * scale + offsetY;
                r = l + op.ClipBounds.Width * scale;
                b = t + op.ClipBounds.Height * scale;
            }
            if (viewportClip is Rect vc)
            {
                l = Math.Max(l, vc.X);
                t = Math.Max(t, vc.Y);
                r = Math.Min(r, vc.X + vc.Width);
                b = Math.Min(b, vc.Y + vc.Height);
            }
            clipX = l;
            clipY = t;
            clipW = Math.Max(0f, r - l);
            clipH = Math.Max(0f, b - t);
        }

        face.TryGetGlyph(0x0020, out uint spaceGid);

        for (int i = 0; i < op.GlyphIds.Length; i++)
        {
            ushort glyphId = op.GlyphIds[i];
            if (glyphId == spaceGid)
            {
                continue;
            }

            float gx = op.Positions[i * 2] * scale + offsetX;
            float gy = op.Positions[i * 2 + 1] * scale + offsetY;
            RenderGlyph(face, glyphId, gx, gy, textR, textG, textB, textA, pixels, width, height, TextGamma, LightWeight, clipX, clipY, clipW, clipH);
        }
    }

    private static void RenderGlyph(FontFace face, ushort glyphId, float x, float y,
        byte textR, byte textG, byte textB, byte textA,
        Span<byte> pixels, int width, int height, float textGamma, float lightWeight,
        float clipX = 0, float clipY = 0, float clipW = float.MaxValue, float clipH = float.MaxValue)
    {
        // WP-3537: foreground luminance for the contrast-adaptive weight; the local
        // background luminance is read per-pixel from the destination in the blend loop.
        float fgLum = (0.2126f * textR + 0.7152f * textG + 0.0722f * textB) / 255f;
        float subpixelX = x - (int)x;
        GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, subpixelX);
        if (gw <= 0 || gh <= 0)
        {
            return;
        }

        // Custom rasterizer expands width by 1 when subpixel shift > 0,
        // so allocate a buffer large enough to hold the expanded bitmap.
        int bufSize = (gw + 1) * gh;
        byte[]? rented = ArrayPool<byte>.Shared.Rent(bufSize);
        Span<byte> bitmap = rented.AsSpan(0, bufSize);

        try
        {
            GlyphRasterizer.Rasterize(face, glyphId, subpixelX, bitmap, out int rw, out int rh, out int minX, out int minY);
            if (rw <= 0 || rh <= 0)
            {
                return;
            }
            // Match the GPU placement convention (BuildGlyphInstances):
            // quad left = pen + minX, quad top = baseline − (minY + rh).
            int startX = (int)x + minX;
            int startY = (int)y - (minY + rh);

            // Check against clip bounds
            float clipRight = clipX + clipW;
            float clipBottom = clipY + clipH;
            if (startX + rw <= clipX || startX >= clipRight ||
                startY + rh <= clipY || startY >= clipBottom)
            {
                return;
            }

            for (int row = 0; row < rh; row++)
            {
                int py = startY + row;
                if (py < 0 || py >= height)
                {
                    continue;
                }
                for (int col = 0; col < rw; col++)
                {
                    int px = startX + col;
                    if (px < 0 || px >= width)
                    {
                        continue;
                    }

                    // The rasterizer stores rows bottom-up (row 0 = bottom of
                    // the glyph); screen rows run top-down, so flip on read.
                    byte coverage = bitmap[(rh - 1 - row) * rw + col];
                    if (coverage == 0)
                    {
                        continue;
                    }

                    // WP-3526/3537: apply the shared contrast-adaptive text-weight
                    // curve so the CPU fallback matches the GPU glyph shader. The CPU
                    // blends in naïve sRGB and already has the destination pixel, so it
                    // reads the local background directly (the GPU samples a framebuffer
                    // copy for the same value). The weighted coverage is the blend alpha.
                    int idx = (py * width + px) * 4;
                    float bgLum = (0.2126f * pixels[idx] + 0.7152f * pixels[idx + 1] + 0.0722f * pixels[idx + 2]) / 255f;
                    float weighted = EtchGpuPresenter.AdaptiveInkCoverage(coverage / 255f, textGamma, fgLum, bgLum, lightWeight);
                    float alpha = weighted * (textA / 255f);
                    if (alpha <= 0)
                    {
                        continue;
                    }

                    float invAlpha = 1f - alpha;

                    pixels[idx] = (byte)(textR * alpha + pixels[idx] * invAlpha);
                    pixels[idx + 1] = (byte)(textG * alpha + pixels[idx + 1] * invAlpha);
                    pixels[idx + 2] = (byte)(textB * alpha + pixels[idx + 2] * invAlpha);
                    pixels[idx + 3] = (byte)(textA * alpha + pixels[idx + 3] * invAlpha);
                }
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    internal FontFace? GetOrCreateFontFace(ulong fontHandle, float fontSize)
    {
        if (!_fonts.TryGetValue(fontHandle, out var entry))
        {
            return null;
        }
        if (entry.Faces.TryGetValue(fontSize, out var face))
        {
            return face;
        }
        var newFace = FontFace.Load(entry.Data, entry.UnitsPerEm, fontSize);
        // Slight (vertical-only) hinting preserves thin stroke junctions that
        // full grid-fitting collapses (e.g. the stem/arch join of 'n' in Inter).
        // This matches the default used by Skia in Chrome and Flutter.
        newFace.Hinting = global::Etch.Text.FontHinting.Slight;
        entry.Faces[fontSize] = newFace;
        return newFace;
    }

    private static unsafe int ReadUnitsPerEm(ReadOnlySpan<byte> data)
    {
        fixed (byte* ptr = data)
        {
            using var blob = new HB.Blob((nint)ptr, data.Length, HB.MemoryMode.ReadOnly, null!);
            using var face = new HB.Face(blob, 0);
            return face.UnitsPerEm;
        }
    }

    private static (byte R, byte G, byte B, byte A) ExtractSrgbBytes(ColorValue c)
    {
        uint argb = ToArgb(c);
        return (
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF),
            (byte)((argb >> 24) & 0xFF)
        );
    }

    internal BezPath? GetCompiledPath(ulong handle) => _compiledPaths.TryGetValue(handle, out var p) ? p : null;

    // ════════════════════════════════════════════════════════════════
    // Op types
    // ════════════════════════════════════════════════════════════════

    internal enum OpKind
    {
        DrawRect, DrawRectGradient, DrawPath, DrawPathGradient, DrawCircle, DrawSector, DrawArc, DrawLine,
        PushTransform, PopTransform, PushClip, PushClipPath, PushClipRoundedRect, PopClip,
        DrawImage,
        // Flutter-style layer texture compositing
        PushLayerTexture, PopLayerTexture, DrawLayerTexture,
        // Frosted-glass backdrop blur: blurs the framebuffer behind a rounded rect.
        // Rendered by a dedicated presenter pass (not the SceneBuffer), so skipped
        // in AppendSceneOp. Coords are baked to device space at emission.
        DrawBackdropBlur,
    }

    internal sealed class SceneOp
    {
        public OpKind Kind;
        public float X, Y, W, H, Radius, InnerRadius, StartRad, SweepRad, StrokeWidth;
        public float G0, G1, G2, G3;
        public int GradientKind;
        public ColorValue? Fill, StrokeColor;
        public ulong PathHandle;
        public Matrix3x2 Matrix;
        public GradientStop[]? GradientStops;
        public ulong ImageHandle;
        public float Opacity;

        /// <summary>
        /// DevTools node id of the node that emitted this op, or null. Only
        /// populated while an MCP session has draw-provenance capture enabled.
        /// </summary>
        public string? DebugNodeId;

        /// <summary>
        /// For <see cref="OpKind.DrawLayerTexture"/>: the screen-space clip rect
        /// active when the layer is composited (the ScrollView viewport). The
        /// presenter intersects composited layer content with this so retained
        /// layers don't bleed outside their viewport (WP-3517). Zero/unset when
        /// <see cref="HasClipBounds"/> is false.
        /// </summary>
        public Rect ClipBounds;
        public bool HasClipBounds;
    }

    internal sealed class GlyphOp
    {
        public ushort[] GlyphIds = [];
        public float[] Positions = [];
        public float FontSize;
        public ulong FontHandle;
        public ColorValue Color;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public Rect ClipBounds;
        public bool HasClipBounds;

        /// <summary>
        /// DevTools node id of the node that emitted this op, or null. Only
        /// populated while an MCP session has draw-provenance capture enabled.
        /// </summary>
        public string? DebugNodeId;
    }

    // ════════════════════════════════════════════════════════════════
    // Path helpers
    // ════════════════════════════════════════════════════════════════

    internal static BezPath BuildRoundedRectPath(float x, float y, float w, float h, float r)
    {
        if (r <= 0)
        {
            return BuildRectPath(x, y, w, h);
        }
        float rx = Math.Min(r, w * 0.5f);
        float ry = Math.Min(r, h * 0.5f);

        // When the rounded rect is actually a circle (radius equals half the side),
        // use the dedicated circle path. This avoids zero-length line segments
        // that confuse path flattening in both strip-coverage and geometry pipelines.
        if (Math.Abs(rx - w * 0.5f) < 0.001f && Math.Abs(ry - h * 0.5f) < 0.001f && Math.Abs(w - h) < 0.001f)
        {
            return BuildCirclePath(x + w * 0.5f, y + h * 0.5f, w * 0.5f);
        }

        float k = 0.5522847498f;

        return new BezPath(
            new byte[] { 0, 1, 3, 1, 3, 1, 3, 1, 3, 4 },
            new double[]
            {
                x + rx, y,
                x + w - rx, y,
                x + w - rx + rx * k, y, x + w, y + ry - ry * k, x + w, y + ry,
                x + w, y + h - ry,
                x + w, y + h - ry + ry * k, x + w - rx + rx * k, y + h, x + w - rx, y + h,
                x + rx, y + h,
                x + rx - rx * k, y + h, x, y + h - ry + ry * k, x, y + h - ry,
                x, y + ry,
                x, y + ry - ry * k, x + rx - rx * k, y, x + rx, y,
            },
            10);
    }

    internal static BezPath BuildRectPath(float x, float y, float w, float h)
        => new(
            new byte[] { 0, 1, 1, 1, 1, 4 },
            new double[] { x, y, x + w, y, x + w, y + h, x, y + h, x, y },
            6);

    internal static BezPath BuildCirclePath(float cx, float cy, float r)
    {
        float k = 0.5522847498f * r;
        return new BezPath(
            new byte[] { 0, 3, 3, 3, 3, 4 },
            new double[]
            {
                cx + r, cy,
                cx + r, cy - k, cx + k, cy - r, cx, cy - r,
                cx - k, cy - r, cx - r, cy - k, cx - r, cy,
                cx - r, cy + k, cx - k, cy + r, cx, cy + r,
                cx + k, cy + r, cx + r, cy + k, cx + r, cy,
            },
            6);
    }

    internal static BezPath BuildLinePath(float x1, float y1, float x2, float y2)
        => new(
            new byte[] { 0, 1 },
            new double[] { x1, y1, x2, y2 },
            2);

    internal static BezPath BuildArcPath(float cx, float cy, float r, float startRad, float sweepRad)
    {
        // Compute segment count for sub-pixel chord height accuracy.
        // Chord height error for radius r and segment angle a: e = r * (1 - cos(a/2))
        // Solve for a: a = 2 * acos(1 - e/r). With e = 0.5 (half pixel):
        float maxSagitta = 0.5f;
        float cosHalfAngle = MathF.Max(-1.0f, MathF.Min(1.0f, 1.0f - maxSagitta / r));
        float anglePerSegment = 2.0f * MathF.Acos(cosHalfAngle);
        int segments = Math.Min(512, Math.Max(4, (int)MathF.Ceiling(MathF.Abs(sweepRad) / anglePerSegment)));

        var coords = new double[2 + segments * 2];
        coords[0] = cx + r * MathF.Cos(startRad);
        coords[1] = cy + r * MathF.Sin(startRad);
        for (int i = 1; i <= segments; i++)
        {
            float angle = startRad + sweepRad * i / segments;
            int idx = 2 * i;
            coords[idx] = cx + r * MathF.Cos(angle);
            coords[idx + 1] = cy + r * MathF.Sin(angle);
        }

        var verbs = new byte[1 + segments];
        verbs[0] = 0;
        for (int i = 0; i < segments; i++)
        {
            verbs[1 + i] = 1;
        }

        return new BezPath(verbs, coords, verbs.Length);
    }

    private static BezPath ConvertCascadePath(ReadOnlySpan<byte> commands, ReadOnlySpan<float> data)
    {
        var verbs = new List<byte>(); var coords = new List<double>(); int di = 0;
        for (int ci = 0; ci < commands.Length && commands[ci] != 0xFF; ci++)
        {
            switch (commands[ci])
            {
                case 0x00: // MoveTo
                    if (di + 2 > data.Length)
                    {
                        break;
                    }
                    verbs.Add(0); coords.Add(data[di++]); coords.Add(data[di++]);
                    break;
                case 0x01: // LineTo
                    if (di + 2 > data.Length)
                    {
                        break;
                    }
                    verbs.Add(1); coords.Add(data[di++]); coords.Add(data[di++]);
                    break;
                case 0x02: // CubicTo
                    if (di + 6 > data.Length)
                    {
                        break;
                    }
                    verbs.Add(3); for (int j = 0; j < 6; j++) { coords.Add(data[di++]); }
                    break;
                case 0x03: // QuadTo
                    if (di + 4 > data.Length)
                    {
                        break;
                    }
                    verbs.Add(2); coords.Add(data[di++]); coords.Add(data[di++]); coords.Add(data[di++]); coords.Add(data[di++]);
                    break;
                case 0x04: // Close
                    verbs.Add(4); break;
            }
        }
        return new BezPath(verbs.ToArray(), coords.ToArray(), verbs.Count);
    }

    /// <summary>
    /// Converts a Cascade <see cref="ColorValue"/> (premultiplied linear sRGB)
    /// to an Etch <c>uint</c> paint color (premultiplied linear BGRA in memory).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ToArgb(ColorValue c)
    {
        float a = Math.Clamp(c.A, 0f, 1f);
        if (a <= 0f)
        {
            return 0;
        }
        // Etch expects linear values, not gamma-encoded sRGB.
        // The uint is stored little-endian as [B,G,R,A] — i.e. linear BGRA.
        float ra = Math.Clamp(c.R / a, 0f, 1f), ga = Math.Clamp(c.G / a, 0f, 1f), ba = Math.Clamp(c.B / a, 0f, 1f);
        return (uint)(((int)(a * 255f + 0.5f) << 24) | ((int)(ra * 255f + 0.5f) << 16) | ((int)(ga * 255f + 0.5f) << 8) | (int)(ba * 255f + 0.5f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Affine ToAffine(Matrix3x2 m)
    {
        // Matrix3x2 uses row-vector convention (x' = x*M11 + y*M21 + M31),
        // while Etch Affine uses column-vector convention (x' = M00*x + M01*y + M02).
        // The linear part must be transposed to preserve transform semantics.
        return new Affine(m.M11, m.M21, m.M12, m.M22, m.M31, m.M32);
    }

    internal static GradientStops ConvertGradientStops(Cascade.UI.GradientStop[] stops)
    {
        var etStops = new (float, uint)[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            etStops[i] = (stops[i].Offset, ToArgb(stops[i].Color));
        }
        return GradientStops.Create(etStops);
    }
}

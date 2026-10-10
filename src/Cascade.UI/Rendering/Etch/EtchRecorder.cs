using System;
using System.Collections.Generic;
using System.Numerics;
using Etch.Compose;
using Etch.Compose.Coverage;
using EScene = Etch.Scene;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Translates the backend's command stream into Etch <see cref="DrawRecording"/>s — the one input
/// both composers render from. Every op maps to the Etch draw that renders it exactly (rects,
/// rounded rects, borders, circles, sectors, lines and arcs with their caps, paths with their joins
/// and dashes, gradients with their geometry, shadows, images, backdrop blurs), glyph runs are
/// interleaved where they were painted, and each retained layer (ScrollView content) is recorded
/// once when it is captured and replayed at its scroll offset on every frame that composites it.
/// </summary>
internal sealed class EtchRecorder
{
    private readonly Stack<Matrix3x2> transformStack = new();
    private readonly Dictionary<ulong, LayerEntry> layers = new();
    private readonly HashSet<ulong> reachable = new();
    private readonly Stack<ulong> pending = new();
    private readonly List<ulong> dead = new();
    private readonly Dictionary<ulong, ComposeGradientStop[]> gradientStops = new();
    private const int MaxCachedGradients = 512;
    private Matrix3x2 current;
    private bool transformDirty;

    private sealed class LayerEntry
    {
        public readonly DrawRecording Recording = new();
        public readonly HashSet<ulong> Children = new();
    }

    /// <summary>Provenance for a recorded glyph run (only built while draw capture is on).</summary>
    internal sealed record GlyphRunSource(EtchBackend.GlyphOp Op, ulong LayerHandle);

    /// <summary>Ops the recorder did not know how to draw since construction (a parity gap).</summary>
    public int UnhandledOps { get; private set; }

    /// <summary>Number of retained layer recordings held.</summary>
    public int LayerCount => layers.Count;

    /// <summary>
    /// Records the frame: the background, then the main command stream into <paramref name="main"/>
    /// (cleared first). Layers captured this frame are (re)recorded first; layers neither captured
    /// nor reachable from this frame are forgotten.
    /// </summary>
    public void RecordFrame(EtchBackend backend, DrawRecording main, ColorValue background, uint width, uint height, bool provenance)
    {
        // Recording objects for every captured layer exist before any is filled, so a layer
        // composited inside another resolves regardless of capture order.
        foreach (var (handle, _) in backend.LayerCaptures)
        {
            if (!layers.ContainsKey(handle))
            {
                layers[handle] = new LayerEntry();
            }
        }
        foreach (var (handle, capture) in backend.LayerCaptures)
        {
            var entry = layers[handle];
            entry.Recording.Clear();
            entry.Children.Clear();
            RecordStream(backend, capture.Commands, capture.GlyphCommands, capture.InitialTransform, entry.Recording, entry.Children, handle, provenance);
        }

        main.Clear();
        main.SetTransform(global::Etch.Geometry.Affine.Identity);
        main.FillRect(0, 0, width, height, ComposePaint.Solid(Straight(background)));
        var mainChildren = reachable;
        mainChildren.Clear();
        RecordStream(backend, backend.Commands, backend.GlyphCommands, Matrix3x2.Identity, main, mainChildren, 0, provenance);
        PruneUnreachable();
    }

    // Keeps the layers the main stream composites, transitively through nested layers.
    private void PruneUnreachable()
    {
        pending.Clear();
        foreach (ulong handle in reachable)
        {
            pending.Push(handle);
        }
        while (pending.Count > 0)
        {
            ulong handle = pending.Pop();
            if (layers.TryGetValue(handle, out var entry))
            {
                foreach (ulong child in entry.Children)
                {
                    if (reachable.Add(child))
                    {
                        pending.Push(child);
                    }
                }
            }
        }

        dead.Clear();
        foreach (ulong handle in layers.Keys)
        {
            if (!reachable.Contains(handle))
            {
                dead.Add(handle);
            }
        }
        foreach (ulong handle in dead)
        {
            layers.Remove(handle);
        }
    }

    private void RecordStream(EtchBackend backend, List<EtchBackend.SceneOp> ops, List<EtchBackend.GlyphOp> glyphs,
        Matrix3x2 initial, DrawRecording recording, HashSet<ulong> children, ulong layerHandle, bool provenance)
    {
        transformStack.Clear();
        current = initial;
        transformDirty = true;
        int glyphCursor = 0;

        for (int k = 0; k <= ops.Count; k++)
        {
            // Glyph runs painted before command k (after command k − 1).
            while (glyphCursor < glyphs.Count && glyphs[glyphCursor].CommandIndex <= k)
            {
                RecordGlyphRun(backend, glyphs[glyphCursor++], recording, layerHandle, provenance);
            }
            if (k == ops.Count)
            {
                break;
            }
            RecordOp(backend, ops[k], recording, children);
        }
    }

    private void SyncTransform(DrawRecording recording)
    {
        if (!transformDirty)
        {
            return;
        }
        recording.SetTransform(EtchBackend.ToAffine(current));
        transformDirty = false;
    }

    private void RecordOp(EtchBackend backend, EtchBackend.SceneOp op, DrawRecording recording, HashSet<ulong> children)
    {
        switch (op.Kind)
        {
            case EtchBackend.OpKind.PushTransform:
                transformStack.Push(current);
                current = op.Matrix * current;
                transformDirty = true;
                return;

            case EtchBackend.OpKind.PopTransform:
                if (transformStack.Count > 0)
                {
                    current = transformStack.Pop();
                    transformDirty = true;
                }
                return;

            case EtchBackend.OpKind.PushLayerTexture:
            case EtchBackend.OpKind.PopLayerTexture:
                // Backend-level scoping only; never emitted into a stream.
                return;
        }

        SyncTransform(recording);
        switch (op.Kind)
        {
            case EtchBackend.OpKind.PushClip:
                recording.PushClipRect(op.X, op.Y, op.W, op.H);
                break;

            case EtchBackend.OpKind.PushClipRoundedRect:
                recording.PushClipRoundedRect(op.X, op.Y, op.W, op.H, op.Radius);
                break;

            case EtchBackend.OpKind.PushClipPath:
                {
                    var path = backend.GetCompiledPath(op.PathHandle);
                    if (path.HasValue)
                    {
                        recording.PushClipPath(path.Value, EScene.FillRule.NonZero);
                    }
                    else
                    {
                        // Keep push/pop balanced: an unknown path clips nothing.
                        recording.PushClipRect(-1e7f, -1e7f, 2e7f, 2e7f);
                    }
                    break;
                }

            case EtchBackend.OpKind.PopClip:
                recording.PopClip();
                break;

            case EtchBackend.OpKind.DrawRect:
                RecordRect(op, recording);
                break;

            case EtchBackend.OpKind.DrawRectGradient:
                if (op.GradientStops is { Length: > 0 })
                {
                    var paint = GradientPaint(op);
                    float r = Math.Min(op.Radius, Math.Min(op.W, op.H) * 0.5f);
                    if (r > 0.5f)
                    {
                        recording.FillRoundedRect(op.X, op.Y, op.W, op.H, r, paint);
                    }
                    else
                    {
                        recording.FillRect(op.X, op.Y, op.W, op.H, paint);
                    }
                }
                break;

            case EtchBackend.OpKind.DrawPath:
            case EtchBackend.OpKind.DrawPathGradient:
                {
                    var path = backend.GetCompiledPath(op.PathHandle);
                    if (!path.HasValue)
                    {
                        break;
                    }
                    if (op.Kind == EtchBackend.OpKind.DrawPathGradient)
                    {
                        if (op.GradientStops is { Length: > 0 })
                        {
                            recording.FillPath(path.Value, EScene.FillRule.NonZero, GradientPaint(op));
                        }
                    }
                    else if (op.Fill is ColorValue fill && fill.A > 0)
                    {
                        recording.FillPath(path.Value, EScene.FillRule.NonZero, ComposePaint.Solid(Straight(fill)));
                    }
                    if (op.StrokeColor is ColorValue stroke && op.StrokeWidth > 0)
                    {
                        recording.StrokePath(path.Value, StrokeOf(op), ComposePaint.Solid(Straight(stroke)));
                    }
                    break;
                }

            case EtchBackend.OpKind.DrawCircle:
                if (op.Fill is ColorValue circleFill && circleFill.A > 0)
                {
                    recording.FillCircle(op.X, op.Y, op.Radius, ComposePaint.Solid(Straight(circleFill)));
                }
                if (op.StrokeColor is ColorValue circleStroke && op.StrokeWidth > 0)
                {
                    recording.StrokeCircle(op.X, op.Y, op.Radius, op.StrokeWidth, Straight(circleStroke));
                }
                break;

            case EtchBackend.OpKind.DrawSector:
                if (op.Fill is ColorValue sectorFill && sectorFill.A > 0)
                {
                    recording.FillSector(op.X, op.Y, op.Radius, op.InnerRadius, op.StartRad, op.SweepRad, Straight(sectorFill));
                }
                break;

            case EtchBackend.OpKind.DrawArc:
                if (op.StrokeColor is ColorValue arcStroke && op.StrokeWidth > 0)
                {
                    recording.StrokeArc(op.X, op.Y, op.Radius, op.StartRad, op.SweepRad, StrokeOf(op), Straight(arcStroke));
                }
                break;

            case EtchBackend.OpKind.DrawLine:
                if (op.StrokeColor is ColorValue lineStroke && op.StrokeWidth > 0)
                {
                    recording.StrokeLine(op.X, op.Y, op.W, op.H, StrokeOf(op), Straight(lineStroke));
                }
                break;

            case EtchBackend.OpKind.DrawShadow:
                if (op.Fill is ColorValue shadow && shadow.A > 0 && op.W > 0 && op.H > 0)
                {
                    recording.Shadow(op.X, op.Y, op.W, op.H, op.Radius, op.StrokeWidth, Straight(shadow));
                }
                break;

            case EtchBackend.OpKind.DrawBackdropBlur:
                if (op.Fill is ColorValue tint && op.W > 0 && op.H > 0)
                {
                    recording.BackdropBlur(op.X, op.Y, op.W, op.H, op.Radius, op.StrokeWidth, Straight(tint));
                }
                break;

            case EtchBackend.OpKind.DrawImage:
                {
                    var image = backend.GetImage(op.ImageHandle);
                    if (image != null)
                    {
                        recording.Image((int)op.ImageHandle, image.Image, op.X, op.Y, op.W, op.H, op.Opacity);
                    }
                    break;
                }

            case EtchBackend.OpKind.DrawLayerTexture:
                {
                    ulong handle = (ulong)op.W;
                    if (layers.TryGetValue(handle, out var layer))
                    {
                        // The layer's content is recorded where it was captured (its initial
                        // transform), so the composite shifts it by the device offset of the
                        // composite point from the local origin — the scroll delta, for a ScrollView.
                        float dx = op.X - op.G0;
                        float dy = op.Y - op.G1;
                        recording.Layer(layer.Recording, dx, dy, op.Opacity);
                        children.Add(handle);
                    }
                    break;
                }

            default:
                UnhandledOps++;
                System.Diagnostics.Debug.Assert(false, $"EtchRecorder has no translation for {op.Kind}");
                break;
        }
    }

    private static void RecordRect(EtchBackend.SceneOp op, DrawRecording recording)
    {
        float r = Math.Max(0f, Math.Min(op.Radius, Math.Min(op.W, op.H) * 0.5f));
        // A transparent fill is still recorded: Etch gives it its place in paint order without drawing
        // it, so a caret blinking off (or a fill fading through zero) changes only its own pixels.
        if (op.Fill is ColorValue fill)
        {
            var paint = ComposePaint.Solid(Straight(fill));
            if (r > 0.5f)
            {
                recording.FillRoundedRect(op.X, op.Y, op.W, op.H, r, paint);
            }
            else
            {
                recording.FillRect(op.X, op.Y, op.W, op.H, paint);
            }
        }
        if (op.StrokeColor is ColorValue stroke && op.StrokeWidth > 0)
        {
            recording.Border(op.X, op.Y, op.W, op.H, r, op.StrokeWidth, Straight(stroke));
        }
    }

    private static void RecordGlyphRun(EtchBackend backend, EtchBackend.GlyphOp op, DrawRecording recording, ulong layerHandle, bool provenance)
    {
        float rasterSize = op.FontSize * Math.Max(op.ScaleX, op.ScaleY);
        var face = backend.GetOrCreateFontFace(op.FontHandle, rasterSize);
        if (face == null || op.GlyphIds.Length == 0)
        {
            return;
        }
        recording.Glyphs(new GlyphRunData(face, (int)op.FontHandle, op.GlyphIds, op.Positions, rasterSize, Straight(op.Color),
            provenance ? new GlyphRunSource(op, layerHandle) : null));
    }

    private static StrokeParameters StrokeOf(EtchBackend.SceneOp op)
    {
        var cap = (EScene.StrokeCap)(int)op.Cap;
        var join = (EScene.StrokeJoin)(int)op.Join;
        if (op.Dash is DashPattern dash && dash.On > 0 && dash.Off > 0)
        {
            return new StrokeParameters(op.StrokeWidth, cap, join, 4f, dash.On, dash.Off, dash.Offset);
        }
        return StrokeParameters.Solid(op.StrokeWidth, cap, join);
    }

    private ComposePaint GradientPaint(EtchBackend.SceneOp op)
    {
        var stops = ConvertStops(op.GradientStops!);
        return op.GradientKind switch
        {
            1 => ComposePaint.Radial(op.G0, op.G1, op.G2, stops),
            2 => ComposePaint.Sweep(op.G0, op.G1, op.G2, stops),
            _ => ComposePaint.Linear(op.G0, op.G1, op.G2, op.G3, stops),
        };
    }

    /// <summary>
    /// The stops in Etch's form, cached by content: a gradient drawn every frame converts once.
    /// Recordings keep the returned array, so a cached array is never written after it is stored.
    /// </summary>
    private ComposeGradientStop[] ConvertStops(GradientStop[] source)
    {
        ulong key = 14695981039346656037UL;
        for (int i = 0; i < source.Length; i++)
        {
            var c = source[i].Color;
            key = Mix(key, BitConverter.SingleToUInt32Bits(source[i].Offset));
            key = Mix(key, BitConverter.SingleToUInt32Bits(c.R));
            key = Mix(key, BitConverter.SingleToUInt32Bits(c.G));
            key = Mix(key, BitConverter.SingleToUInt32Bits(c.B));
            key = Mix(key, BitConverter.SingleToUInt32Bits(c.A));
        }
        if (gradientStops.TryGetValue(key, out var cached) && SameStops(cached, source))
        {
            return cached;
        }
        var stops = new ComposeGradientStop[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            stops[i] = new ComposeGradientStop(source[i].Offset, Straight(source[i].Color));
        }
        if (gradientStops.Count >= MaxCachedGradients)
        {
            gradientStops.Clear();
        }
        gradientStops[key] = stops;
        return stops;
    }

    private static bool SameStops(ComposeGradientStop[] cached, GradientStop[] source)
    {
        if (cached.Length != source.Length)
        {
            return false;
        }
        for (int i = 0; i < source.Length; i++)
        {
            if (cached[i] != new ComposeGradientStop(source[i].Offset, Straight(source[i].Color)))
            {
                return false;
            }
        }
        return true;
    }

    private static ulong Mix(ulong hash, uint value) => (hash ^ value) * 1099511628211UL;

    /// <summary>Converted gradients held (tests read it).</summary>
    internal int CachedGradientCount => gradientStops.Count;

    /// <summary>Premultiplied <see cref="ColorValue"/> → straight-alpha linear colour.</summary>
    internal static ComposeColor Straight(ColorValue c)
    {
        float a = Math.Clamp(c.A, 0f, 1f);
        if (a <= 0f)
        {
            return ComposeColor.Transparent;
        }
        return new ComposeColor(c.R / a, c.G / a, c.B / a, a);
    }
}

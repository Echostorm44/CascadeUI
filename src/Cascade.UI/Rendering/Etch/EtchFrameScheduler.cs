using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Etch.Compose;
using Etch.Scene;
using Etch.Text.Atlas;
using Etch.Text.Outline;
using Etch.Text.Rasterize;
using EGeometry = Etch.Geometry;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Builds a frame's <see cref="DrawList"/> from the backend's command stream: shape instances from
/// the scene, glyph runs, images and backdrop blurs in paint order, with each retained layer spliced
/// in at its <c>DrawLayerTexture</c> op. The one producer both composers execute — the GPU presenter
/// and the CPU fallback build their frames here, so they draw the same items in the same order.
/// </summary>
internal sealed class EtchFrameScheduler
{
    private static readonly bool GlyphDumpEnabled = DebugLog.IsEnabled(DebugLogCategory.Glyph);

    // Main-scene shape instances, rebuilt only when the scene object changes.
    private SceneBuffer? lastScene;
    private readonly List<ShapeInstance> mainInstances = new();
    private readonly List<int> mainInstanceAt = new();

    // Layer instance cache — keyed by layer handle; the scene reference invalidates it on recapture.
    // InstanceAt maps the layer's captured command index to its shape-instance index (paint order).
    private readonly Dictionary<ulong, (SceneBuffer Scene, List<ShapeInstance> Instances, List<int> InstanceAt)> layerInstanceCache = new();
    private readonly HashSet<ulong> activeLayerHandles = new();
    private readonly List<ulong> staleLayerHandles = new();

    // Main-stream image replay state (transform and device-space clip stacks).
    private readonly Stack<Matrix3x2> imageTransformStack = new();
    private readonly Stack<(float L, float T, float R, float B, bool Constrains)> imageClipStack = new();

    /// <summary>Glyphs culled by their clip in the last built frame.</summary>
    public int CulledGlyphs { get; private set; }

    /// <summary>
    /// Builds <paramref name="list"/> for one frame. <paramref name="mono"/> and
    /// <paramref name="color"/> are the atlases of the composer that will execute it; glyph UVs refer to them.
    /// </summary>
    public void Build(DrawList list, SceneBuffer scene, EtchBackend backend, IReadOnlyList<int>? sceneMarks,
        List<LayerRenderInfo>? layers, List<GlyphDrawRecord>? glyphRecords, GlyphAtlas mono, GlyphAtlas color,
        uint width, uint height)
    {
        PruneLayerCache(layers);

        if (!ReferenceEquals(scene, lastScene))
        {
            lastScene = scene;
            ShapeInstanceBuilder.Build(scene, mainInstances, sceneMarks, mainInstanceAt);
        }

        if (layers != null)
        {
            foreach (var layer in layers)
            {
                bool needsRebuild = !layerInstanceCache.TryGetValue(layer.LayerHandle, out var cached)
                    || !ReferenceEquals(cached.Scene, layer.Scene);
                if (!needsRebuild)
                {
                    continue;
                }

                var rebuiltInstances = cached.Instances ?? new List<ShapeInstance>();
                var rebuiltInstanceAt = cached.InstanceAt ?? new List<int>();
                ShapeInstanceBuilder.Build(layer.Scene, rebuiltInstances, layer.SceneMarks, rebuiltInstanceAt);
                layerInstanceCache[layer.LayerHandle] = (layer.Scene, rebuiltInstances, rebuiltInstanceAt);
                LogLayerInstances(layer, rebuiltInstances);
            }
        }

        list.Reset(width, height);
        Schedule(list, backend, layers, glyphRecords, mono, color);
    }

    /// <summary>Forgets cached main-scene instances (the next frame rebuilds them).</summary>
    public void Invalidate()
    {
        lastScene = null;
        layerInstanceCache.Clear();
    }

    private void PruneLayerCache(List<LayerRenderInfo>? layers)
    {
        activeLayerHandles.Clear();
        if (layers != null)
        {
            foreach (var layer in layers)
            {
                activeLayerHandles.Add(layer.LayerHandle);
            }
        }
        if (layerInstanceCache.Count == 0)
        {
            return;
        }

        staleLayerHandles.Clear();
        foreach (var key in layerInstanceCache.Keys)
        {
            if (!activeLayerHandles.Contains(key))
            {
                staleLayerHandles.Add(key);
            }
        }
        foreach (var key in staleLayerHandles)
        {
            layerInstanceCache.Remove(key);
        }
    }

    private static void LogLayerInstances(in LayerRenderInfo layer, List<ShapeInstance> layerInstances)
    {
        if (!DebugLog.IsEnabled(DebugLogCategory.Instance) || layerInstances.Count == 0)
        {
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var inst in layerInstances)
        {
            if (inst.MinX < minX) { minX = inst.MinX; }
            if (inst.MinY < minY) { minY = inst.MinY; }
            if (inst.MaxX > maxX) { maxX = inst.MaxX; }
            if (inst.MaxY > maxY) { maxY = inst.MaxY; }
        }
        DebugLog.Write(DebugLogCategory.Instance,
            $"[{DateTime.Now:O}] Layer {layer.LayerHandle}: {layerInstances.Count} instances, bounds ({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}), offset ({layer.OffsetX:F1},{layer.OffsetY:F1})");
    }

    /// <summary>
    /// The scroll delta to add to a retained layer's baked (absolute) instances when compositing:
    /// <c>offset − clipOrigin</c>. The cached instances already carry the ScrollView's on-screen
    /// origin (baked through the layer's InitialTransform), and so does the composite offset, so
    /// adding the whole offset would double-count it — the residual shift is just the scroll amount.
    /// Shared by the shape, glyph and image composite paths so they can never disagree (RENDER-001).
    /// </summary>
    internal static (float X, float Y) LayerScrollDelta(in LayerRenderInfo layer)
    {
        float dx = layer.OffsetX;
        float dy = layer.OffsetY;
        if (layer.ViewportClip is Cascade.UI.Rect clip)
        {
            dx -= clip.X;
            dy -= clip.Y;
        }
        return (dx, dy);
    }

    /// <summary>
    /// Walks the frame in paint order — the main command stream with each retained layer spliced in
    /// at its <c>DrawLayerTexture</c> op — and places every shape instance, glyph run, image and
    /// backdrop blur in <paramref name="list"/>.
    /// </summary>
    private void Schedule(DrawList list, EtchBackend backend, List<LayerRenderInfo>? layers,
        List<GlyphDrawRecord>? glyphRecords, GlyphAtlas mono, GlyphAtlas color)
    {
        CulledGlyphs = 0;
        imageTransformStack.Clear();
        imageClipStack.Clear();

        var commands = backend.Commands;
        var glyphs = backend.GlyphCommands;
        int shapeCursor = 0;
        int glyphCursor = 0;
        var currentTransform = Matrix3x2.Identity;

        for (int k = 0; k <= commands.Count; k++)
        {
            // Glyph runs painted before command k (after command k − 1).
            while (glyphCursor < glyphs.Count && glyphs[glyphCursor].CommandIndex <= k)
            {
                var op = glyphs[glyphCursor++];
                shapeCursor = EmitShapes(list, mainInstances, shapeCursor, InstanceAt(mainInstanceAt, op.CommandIndex, mainInstances.Count));
                EmitGlyphRun(list, op, backend, 0, 0, null, glyphRecords, "main", DrawPaintOrder.MainGlyphRun(op.CommandIndex), mono, color);
            }

            if (k == commands.Count)
            {
                break;
            }

            var cmd = commands[k];
            switch (cmd.Kind)
            {
                case EtchBackend.OpKind.PushTransform:
                    imageTransformStack.Push(currentTransform);
                    currentTransform = cmd.Matrix * currentTransform;
                    break;

                case EtchBackend.OpKind.PopTransform:
                    if (imageTransformStack.Count > 0)
                    {
                        currentTransform = imageTransformStack.Pop();
                    }
                    break;

                // Active clip rects in DEVICE space: images are clamped to them. Rounded clips use
                // their AABB; path clips push a non-constraining entry so Push/Pop stay balanced.
                case EtchBackend.OpKind.PushClip:
                case EtchBackend.OpKind.PushClipRoundedRect:
                    {
                        var clipLocal = new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
                        var clipDev = clipLocal.Transform(EtchBackend.ToAffine(currentTransform));
                        imageClipStack.Push((
                            (float)clipDev.MinX, (float)clipDev.MinY,
                            (float)clipDev.MaxX, (float)clipDev.MaxY, true));
                        break;
                    }

                case EtchBackend.OpKind.PushClipPath:
                    imageClipStack.Push((0f, 0f, 0f, 0f, false));
                    break;

                case EtchBackend.OpKind.PopClip:
                    if (imageClipStack.Count > 0)
                    {
                        imageClipStack.Pop();
                    }
                    break;

                case EtchBackend.OpKind.DrawImage:
                    shapeCursor = EmitShapes(list, mainInstances, shapeCursor, InstanceAt(mainInstanceAt, k, mainInstances.Count));
                    EmitMainImage(list, backend, cmd, currentTransform);
                    break;

                case EtchBackend.OpKind.DrawBackdropBlur:
                    shapeCursor = EmitShapes(list, mainInstances, shapeCursor, InstanceAt(mainInstanceAt, k, mainInstances.Count));
                    EmitBlur(list, cmd);
                    break;

                case EtchBackend.OpKind.DrawLayerTexture:
                    if (layers != null && TryFindLayer(layers, (ulong)cmd.W, out var layer))
                    {
                        shapeCursor = EmitShapes(list, mainInstances, shapeCursor, InstanceAt(mainInstanceAt, k, mainInstances.Count));
                        EmitLayer(list, backend, layer, k, glyphRecords, mono, color);
                    }
                    break;
            }
        }

        EmitShapes(list, mainInstances, shapeCursor, mainInstances.Count);
    }

    private static bool TryFindLayer(List<LayerRenderInfo> layers, ulong handle, out LayerRenderInfo layer)
    {
        foreach (var candidate in layers)
        {
            if (candidate.LayerHandle == handle)
            {
                layer = candidate;
                return true;
            }
        }
        layer = default;
        return false;
    }

    /// <summary>
    /// Instances emitted before backend command <paramref name="commandIndex"/>. Without marks the
    /// table is empty and every glyph/image falls after all shapes — the old fixed-pass order.
    /// </summary>
    private static int InstanceAt(List<int> instanceAt, int commandIndex, int total)
        => commandIndex < instanceAt.Count ? instanceAt[commandIndex] : total;

    /// <summary>
    /// Composites one retained layer at its paint position: its shapes (scroll delta baked into
    /// the cached absolute positions, clipped to the viewport), glyph runs and images, interleaved
    /// in the layer's own paint order.
    /// </summary>
    private void EmitLayer(DrawList list, EtchBackend backend, in LayerRenderInfo layer, int mainIndex,
        List<GlyphDrawRecord>? glyphRecords, GlyphAtlas mono, GlyphAtlas color)
    {
        if (!layerInstanceCache.TryGetValue(layer.LayerHandle, out var cached))
        {
            return;
        }

        var (dx, dy) = LayerScrollDelta(layer);
        var instances = cached.Instances;
        var instanceAt = cached.InstanceAt;
        var glyphs = layer.GlyphCommands;
        var images = layer.ImageCommands;
        int shapeCursor = 0;
        int glyphCursor = 0;
        int imageCursor = 0;

        while (glyphCursor < glyphs.Count || imageCursor < images.Count)
        {
            // A glyph run with CommandIndex c is painted before command c, so on a tie with the
            // image that IS command c, the run goes first.
            bool glyphNext = imageCursor >= images.Count
                || (glyphCursor < glyphs.Count && glyphs[glyphCursor].CommandIndex <= images[imageCursor].CommandIndex);
            if (glyphNext)
            {
                var op = glyphs[glyphCursor++];
                shapeCursor = EmitLayerShapes(list, instances, shapeCursor, InstanceAt(instanceAt, op.CommandIndex, instances.Count), dx, dy, layer.ViewportClip);
                EmitGlyphRun(list, op, backend, dx, dy, layer.ViewportClip, glyphRecords, "layer", DrawPaintOrder.LayerGlyphRun(mainIndex, op.CommandIndex), mono, color);
            }
            else
            {
                var image = images[imageCursor++];
                shapeCursor = EmitLayerShapes(list, instances, shapeCursor, InstanceAt(instanceAt, image.CommandIndex, instances.Count), dx, dy, layer.ViewportClip);
                EmitLayerImage(list, backend, image, dx, dy, layer.ViewportClip);
            }
        }

        EmitLayerShapes(list, instances, shapeCursor, instances.Count, dx, dy, layer.ViewportClip);
    }

    /// <summary>Places main-stream shape instances [from, to) and returns <paramref name="to"/>.</summary>
    private static int EmitShapes(DrawList list, List<ShapeInstance> source, int from, int to)
    {
        if (to <= from)
        {
            return Math.Max(from, to);
        }

        list.AddShapes(CollectionsMarshal.AsSpan(source).Slice(from, to - from));
        return to;
    }

    /// <summary>
    /// Places retained-layer shape instances [from, to), shifted by the scroll delta and clipped
    /// to the layer viewport, and returns <paramref name="to"/>.
    /// </summary>
    private static int EmitLayerShapes(DrawList list, List<ShapeInstance> source, int from, int to, float dx, float dy, Cascade.UI.Rect? viewportClip)
    {
        for (int i = from; i < to; i++)
        {
            var moved = source[i].Translated(dx, dy);

            // WP-3517: clip only the rasterization extent (quad bounds) to the layer's viewport so
            // scrolled-away content does not bleed outside the ScrollView.
            if (viewportClip is Cascade.UI.Rect vc)
            {
                float minX = Math.Max(moved.MinX, vc.X);
                float minY = Math.Max(moved.MinY, vc.Y);
                float maxX = Math.Min(moved.MaxX, vc.X + vc.Width);
                float maxY = Math.Min(moved.MaxY, vc.Y + vc.Height);
                if (maxX <= minX || maxY <= minY)
                {
                    continue;
                }
                moved = moved with { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
            }

            list.AddShape(moved);
        }
        return Math.Max(from, to);
    }

    /// <summary>Builds one glyph run's instances (mono and colour) and places each part by its bounds.</summary>
    private void EmitGlyphRun(DrawList list, EtchBackend.GlyphOp op, EtchBackend backend, float dx, float dy,
        Cascade.UI.Rect? viewportClip, List<GlyphDrawRecord>? glyphRecords, string category, long paintOrder,
        GlyphAtlas mono, GlyphAtlas color)
    {
        int monoStart = list.Glyphs.Count;
        int colorStart = list.ColorGlyphs.Count;
        int culled = CulledGlyphs;
        BuildGlyphInstances(op, backend, mono, color, list.Glyphs, list.ColorGlyphs, dx, dy,
            ref culled, glyphRecords, category, paintOrder, viewportClip);
        CulledGlyphs = culled;

        list.PlaceGlyphs(DrawKind.Glyph, monoStart);
        list.PlaceGlyphs(DrawKind.ColorGlyph, colorStart);
    }

    private void EmitMainImage(DrawList list, EtchBackend backend, EtchBackend.SceneOp cmd, Matrix3x2 transform)
    {
        var img = backend.GetImage(cmd.ImageHandle);
        if (img == null)
        {
            return;
        }

        // The root DPI-scale PushTransform is part of the stream, so this is device-correct at any DPI.
        var localRect = new EGeometry.Rect(cmd.X, cmd.Y, cmd.X + cmd.W, cmd.Y + cmd.H);
        var deviceRect = localRect.Transform(EtchBackend.ToAffine(transform));
        if (deviceRect.IsEmpty)
        {
            return;
        }

        float cl = float.NegativeInfinity, ct = float.NegativeInfinity;
        float cr = float.PositiveInfinity, cb = float.PositiveInfinity;
        foreach (var c in imageClipStack)
        {
            if (!c.Constrains)
            {
                continue;
            }

            cl = Math.Max(cl, c.L); ct = Math.Max(ct, c.T);
            cr = Math.Min(cr, c.R); cb = Math.Min(cb, c.B);
        }

        bool clipped = !float.IsNegativeInfinity(cl);
        list.AddImage((int)cmd.ImageHandle, img.Image,
            (float)deviceRect.MinX, (float)deviceRect.MinY, (float)deviceRect.MaxX, (float)deviceRect.MaxY,
            clipped, cl, ct, cr, cb);
    }

    private static void EmitLayerImage(DrawList list, EtchBackend backend, in LayerImageOp image, float offX, float offY, Cascade.UI.Rect? viewportClip)
    {
        var img = backend.GetImage(image.ImageHandle);
        if (img == null)
        {
            return;
        }

        var localRect = new EGeometry.Rect(image.X, image.Y, image.X + image.W, image.Y + image.H);
        var deviceRect = localRect.Transform(EtchBackend.ToAffine(image.Transform));
        if (deviceRect.IsEmpty)
        {
            return;
        }

        // Clamp to the viewport AND to whatever clips were in force around the image inside the
        // layer (RENDER-009). The captured clip scrolls with the content; the viewport does not.
        float cl = float.NegativeInfinity, ct = float.NegativeInfinity;
        float cr = float.PositiveInfinity, cb = float.PositiveInfinity;
        if (image.Clip is Cascade.UI.Rect ic)
        {
            cl = ic.X + offX; ct = ic.Y + offY;
            cr = ic.X + ic.Width + offX; cb = ic.Y + ic.Height + offY;
        }
        if (viewportClip is Cascade.UI.Rect vc)
        {
            cl = Math.Max(cl, vc.X); ct = Math.Max(ct, vc.Y);
            cr = Math.Min(cr, vc.X + vc.Width); cb = Math.Min(cb, vc.Y + vc.Height);
        }

        bool clipped = !float.IsNegativeInfinity(cl) || !float.IsNegativeInfinity(ct)
            || !float.IsPositiveInfinity(cr) || !float.IsPositiveInfinity(cb);
        list.AddImage((int)image.ImageHandle, img.Image,
            (float)deviceRect.MinX + offX, (float)deviceRect.MinY + offY,
            (float)deviceRect.MaxX + offX, (float)deviceRect.MaxY + offY,
            clipped, cl, ct, cr, cb);
    }

    private static void EmitBlur(DrawList list, EtchBackend.SceneOp cmd)
    {
        if (!cmd.Fill.HasValue || cmd.W <= 0f || cmd.H <= 0f)
        {
            return;
        }

        var (tr, tg, tb, ta) = PaintColor.ToLinear(EtchBackend.ToArgb(cmd.Fill.Value));
        list.AddBlur(new BlurInstance
        {
            MinX = cmd.X, MinY = cmd.Y,
            MaxX = cmd.X + cmd.W, MaxY = cmd.Y + cmd.H,
            TintR = tr, TintG = tg, TintB = tb, TintA = ta,
            Radius = cmd.Radius, Sigma = cmd.StrokeWidth,
        });
    }

    private static void BuildGlyphInstances(EtchBackend.GlyphOp cmd, EtchBackend backend, GlyphAtlas monoAtlas, GlyphAtlas colorAtlas,
        List<GlyphInstance> instances, List<GlyphInstance> colorInstances, float offsetX, float offsetY, ref int culledGlyphs,
        List<GlyphDrawRecord>? records, string recordCategory, long paintOrder, Cascade.UI.Rect? viewportClip)
    {
        float atlasDim = monoAtlas.Dimension;

        // Rasterize glyphs at physical pixel size so the bitmap is 1:1 with the screen. cmd.ScaleX/Y
        // carry the DPI scale from DrawGlyphs.
        float rasterScale = Math.Max(cmd.ScaleX, cmd.ScaleY);
        float rasterFontSize = cmd.FontSize * rasterScale;

        // Glyphs whose quad falls entirely outside the active clip are skipped. When rendering layer
        // glyphs, offsetX/Y shifts positions to composited coordinates — the clip shifts too.
        bool emptyClip = cmd.HasClipBounds && (cmd.ClipBounds.Width <= 0 || cmd.ClipBounds.Height <= 0);
        if (emptyClip)
        {
            culledGlyphs += cmd.GlyphIds.Length;
            return;
        }
        bool hasClip = cmd.HasClipBounds && cmd.ClipBounds.Width > 0 && cmd.ClipBounds.Height > 0;
        float clipMinX = hasClip ? cmd.ClipBounds.X + offsetX : float.MinValue;
        float clipMinY = hasClip ? cmd.ClipBounds.Y + offsetY : float.MinValue;
        float clipMaxX = hasClip ? cmd.ClipBounds.X + cmd.ClipBounds.Width + offsetX : float.MaxValue;
        float clipMaxY = hasClip ? cmd.ClipBounds.Y + cmd.ClipBounds.Height + offsetY : float.MaxValue;

        // WP-3517: intersect the layer's compositing-time viewport clip (screen space, like the
        // glyph positions) so layer text does not bleed outside the ScrollView.
        if (viewportClip is Cascade.UI.Rect vc)
        {
            clipMinX = Math.Max(clipMinX, vc.X);
            clipMinY = Math.Max(clipMinY, vc.Y);
            clipMaxX = Math.Min(clipMaxX, vc.X + vc.Width);
            clipMaxY = Math.Min(clipMaxY, vc.Y + vc.Height);
            hasClip = true;
        }

        var face = backend.GetOrCreateFontFace(cmd.FontHandle, rasterFontSize);
        if (face == null)
        {
            return;
        }

        int faceId = (int)cmd.FontHandle;
        float colorR = cmd.Color.R;
        float colorG = cmd.Color.G;
        float colorB = cmd.Color.B;
        float colorA = cmd.Color.A;

        face.TryGetGlyph(0x0020, out uint spaceGid);

        for (int i = 0; i < cmd.GlyphIds.Length; i++)
        {
            ushort glyphId = cmd.GlyphIds[i];
            if (glyphId == spaceGid)
            {
                continue;
            }

            float gx = cmd.Positions[i * 2] + offsetX;
            float gy = cmd.Positions[i * 2 + 1] + offsetY;

            byte subpixelQuant = GlyphPlacement.SubpixelBucket(gx);

            bool isColorGlyph = GlyphOutlineBuilder.HasColorLayers(face, glyphId);

            if (isColorGlyph)
            {
                var colorKey = global::Etch.Text.Atlas.GlyphCacheKey.FromSizeAndSubpixel(rasterFontSize, faceId, glyphId, subpixelQuant);
                bool colorCacheHit = colorAtlas.TryLookup(colorKey, out var colorRegion, out int colorPageIndex);
                if (!colorCacheHit)
                {
                    byte[]? rgbaRented = ArrayPool<byte>.Shared.Rent(256 * 256 * 4);
                    try
                    {
                        if (GlyphRasterizer.RasterizeColorGlyph(face, glyphId, rgbaRented.AsSpan(), out int cw, out int ch, out int cminX, out int cminY))
                        {
                            if (cw > 0 && ch > 0)
                            {
                                colorAtlas.TryInsert(colorKey, rgbaRented.AsSpan(0, cw * ch * 4), cw, ch, out colorRegion, out colorPageIndex, (short)cminX, (short)cminY);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rgbaRented);
                    }
                }

                if (colorRegion.W > 0 && colorRegion.H > 0)
                {
                    // Colour glyphs carry no baked subpixel offset and draw through the linear
                    // sampler, so the fractional quad position is their single subpixel source.
                    float cpx = gx + colorRegion.OffsetX;
                    float cpy = gy - (colorRegion.OffsetY + colorRegion.H);

                    if (hasClip && (cpx + colorRegion.W <= clipMinX || cpx >= clipMaxX ||
                                    cpy + colorRegion.H <= clipMinY || cpy >= clipMaxY))
                    {
                        culledGlyphs++;
                        continue;
                    }

                    colorInstances.Add(new GlyphInstance
                    {
                        PosX = cpx,
                        PosY = cpy,
                        SizeX = colorRegion.W,
                        SizeY = colorRegion.H,
                        AtlasU0 = colorRegion.U / atlasDim,
                        AtlasV0 = (colorRegion.V + colorRegion.H) / atlasDim,
                        AtlasU1 = (colorRegion.U + colorRegion.W) / atlasDim,
                        AtlasV1 = colorRegion.V / atlasDim,
                        R = 1.0f,
                        G = 1.0f,
                        B = 1.0f,
                        A = 1.0f,
                        ClipMinX = hasClip ? clipMinX : 0,
                        ClipMinY = hasClip ? clipMinY : 0,
                        ClipMaxX = hasClip ? clipMaxX : 0,
                        ClipMaxY = hasClip ? clipMaxY : 0,
                    });
                    records?.Add(new GlyphDrawRecord(
                        cpx, cpy, colorRegion.W, colorRegion.H,
                        glyphId, rasterFontSize, cmd.FontHandle,
                        colorRegion.U, colorRegion.V, colorRegion.W, colorRegion.H,
                        cmd.Color,
                        hasClip ? clipMinX : 0, hasClip ? clipMinY : 0,
                        hasClip ? clipMaxX : 0, hasClip ? clipMaxY : 0,
                        hasClip, IsColorGlyph: true, recordCategory, cmd.DebugNodeId, paintOrder));
                    continue;
                }
                // Colour glyph rasterization failed — fall through to monochrome.
            }

            var key = global::Etch.Text.Atlas.GlyphCacheKey.FromSizeAndSubpixel(rasterFontSize, faceId, glyphId, subpixelQuant);

            bool cacheHit = monoAtlas.TryLookup(key, out var region, out int pageIndex);
            if (!cacheHit)
            {
                GlyphRasterizer.Measure(face, glyphId, out int gw, out int gh, subpixelQuant / 4f);
                if (gw > 0 && gh > 0)
                {
                    // The rasterizer expands width by 1 when the subpixel shift > 0.
                    int bufSize = (gw + 1) * gh;
                    byte[]? rented = ArrayPool<byte>.Shared.Rent(bufSize);
                    try
                    {
                        GlyphRasterizer.Rasterize(face, glyphId, subpixelQuant / 4f, rented.AsSpan(0, bufSize), out int rw, out int rh, out int minX, out int minY);
                        if (rw > 0 && rh > 0)
                        {
                            if (GlyphDumpEnabled)
                            {
                                DumpGlyphBitmap(rented.AsSpan(0, rw * rh), rw, rh, faceId, glyphId, rasterFontSize, subpixelQuant, gw, gh);
                            }
                            monoAtlas.TryInsert(key, rented.AsSpan(0, rw * rh), rw, rh, out region, out pageIndex, (short)minX, (short)minY);
                            if (GlyphDumpEnabled)
                            {
                                LogGlyphRegion(faceId, glyphId, rasterFontSize, subpixelQuant, pageIndex, region);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            if (region.W == 0 || region.H == 0)
            {
                continue;
            }

            // Bitmap is already at physical size — render 1:1. The horizontal subpixel offset is
            // baked into the bitmap, so the quad sits at the integer pen origin; the baseline snaps
            // to the nearest pixel row (WP-3508).
            float px = GlyphPlacement.QuadOriginX(gx, region.OffsetX);
            float py = GlyphPlacement.QuadOriginY(gy, region.OffsetY, region.H);

            if (hasClip && (px + region.W <= clipMinX || px >= clipMaxX ||
                            py + region.H <= clipMinY || py >= clipMaxY))
            {
                culledGlyphs++;
                continue;
            }

            instances.Add(new GlyphInstance
            {
                PosX = px,
                PosY = py,
                SizeX = region.W,
                SizeY = region.H,
                AtlasU0 = region.U / atlasDim,
                AtlasV0 = (region.V + region.H) / atlasDim,
                AtlasU1 = (region.U + region.W) / atlasDim,
                AtlasV1 = region.V / atlasDim,
                R = colorR,
                G = colorG,
                B = colorB,
                A = colorA,
                ClipMinX = hasClip ? clipMinX : 0,
                ClipMinY = hasClip ? clipMinY : 0,
                ClipMaxX = hasClip ? clipMaxX : 0,
                ClipMaxY = hasClip ? clipMaxY : 0,
            });
            records?.Add(new GlyphDrawRecord(
                px, py, region.W, region.H,
                glyphId, rasterFontSize, cmd.FontHandle,
                region.U, region.V, region.W, region.H,
                cmd.Color,
                hasClip ? clipMinX : 0, hasClip ? clipMinY : 0,
                hasClip ? clipMaxX : 0, hasClip ? clipMaxY : 0,
                hasClip, IsColorGlyph: false, recordCategory, cmd.DebugNodeId, paintOrder));
        }
    }

    private static void LogGlyphRegion(int faceId, ushort glyphId, float fontSize, byte subpixelQuant, int pageIndex, AtlasRegion region)
    {
        DebugLog.Write(DebugLogCategory.Glyph,
            $"face={faceId} glyph={glyphId} size={fontSize} sub={subpixelQuant} page={pageIndex} u={region.U} v={region.V} w={region.W} h={region.H}");
    }

    private static void DumpGlyphBitmap(ReadOnlySpan<byte> bitmap, int w, int h, int faceId, ushort glyphId, float fontSize, byte subpixelQuant, int measuredW, int measuredH)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== face={faceId} glyph={glyphId} size={fontSize} sub={subpixelQuant} rasterized={w}x{h} measured={measuredW}x{measuredH} thread={Environment.CurrentManagedThreadId} ===");
        // Bitmap rows are stored bottom-up; print top-down.
        for (int row = h - 1; row >= 0; row--)
        {
            for (int col = 0; col < w; col++)
            {
                byte v = bitmap[row * w + col];
                sb.Append(v switch
                {
                    0 => '.',
                    < 64 => ':',
                    < 128 => '+',
                    < 192 => '#',
                    _ => '@',
                });
            }
            sb.AppendLine();
        }
        DebugLog.Write(DebugLogCategory.Glyph, sb.ToString().TrimEnd());
    }
}

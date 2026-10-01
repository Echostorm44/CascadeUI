using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cascade.UI.Backend.Etch;

/// <summary>The kinds of draw the GPU presenter issues, each with its own pipeline.</summary>
internal enum DrawKind : byte
{
    Shape,
    Glyph,
    ColorGlyph,
    Image,
    Blur,
    Strip,
}

/// <summary>A run of same-kind draws issued with one pipeline, in device-pixel bounds.</summary>
internal struct DrawBatch
{
    public DrawKind Kind;
    public float MinX, MinY, MaxX, MaxY; // union of everything in the batch
    public int BoxCount; // footprint boxes in use (see PaintOrderBatcher.BoxesPerBatch)
    public int Count;    // elements (instances / quads) in the batch
    public int Start;    // first element in the kind's upload buffer (AssignStarts)
    public int Fill;     // scatter cursor while ordering
}

/// <summary>A contiguous range of one kind's paint-order list that went to one batch.</summary>
internal struct DrawItem
{
    public int Batch;
    public int Start;
    public int Count;
}

/// <summary>
/// Groups a frame's draws into as few same-kind batches as painter's order allows.
/// </summary>
/// <remarks>
/// <para>
/// The GPU presenter draws shapes (SDF quads), glyphs (mono and colour atlas), images (one texture
/// each) and backdrop blurs with different pipelines. Drawing them in fixed passes by kind loses
/// painter's order — every glyph and image lands on top of every shape, so content showed through
/// an opaque panel stacked over it. Drawing strictly in paint order instead would switch pipelines
/// at every label, and since the mono glyph shader reads a copy of the framebuffer for its
/// contrast-adaptive weight, every switch to text would also cost a pass break and a copy.
/// </para>
/// <para>
/// So draws are batched by kind, but a draw only joins a batch if that cannot reorder it against
/// anything it overlaps. Fed in paint order, each draw goes after the last batch whose footprint
/// it overlaps (everything painted earlier that it covers or blends over is in that batch or
/// before it) and joins the first batch of its own kind from there on, or opens a new batch at the
/// end. Draws that do not overlap commute, so the result is exactly painter's order — and an
/// ordinary UI frame (backgrounds, then text and icons on them) still comes out as one shape batch,
/// one glyph batch and one framebuffer copy. Extra batches only appear where something really is
/// painted over text or an image, like a menu panel over content.
/// </para>
/// </remarks>
internal sealed class PaintOrderBatcher
{
    public const int KindCount = 6;

    // A batch's footprint is kept as up to BoxesPerBatch boxes, not one union: a single union of,
    // say, a sidebar's labels and a header's covers the whole content area, and every card drawn
    // there afterwards would look like it overlaps that text and open a needless new batch (and
    // framebuffer copy). A draw that overlaps a box merges into it, so a run of nearby draws stays
    // one box; when every box is taken, the draw merges into the box it grows least.
    public const int BoxesPerBatch = 16;

    private readonly record struct BoundsBox(float MinX, float MinY, float MaxX, float MaxY)
    {
        public bool Overlaps(float minX, float minY, float maxX, float maxY)
            => minX < MaxX && maxX > MinX && minY < MaxY && maxY > MinY;

        public BoundsBox Union(float minX, float minY, float maxX, float maxY)
            => new(Math.Min(MinX, minX), Math.Min(MinY, minY), Math.Max(MaxX, maxX), Math.Max(MaxY, maxY));

        public float Area => Math.Max(0f, MaxX - MinX) * Math.Max(0f, MaxY - MinY);
    }

    private readonly List<DrawBatch> batches = new();
    private readonly List<BoundsBox> boxes = new();
    private readonly int[] lastBatchOfKind = new int[KindCount];
    private readonly bool[] kindInOrder = new bool[KindCount];

    public PaintOrderBatcher()
    {
        Reset();
    }

    /// <summary>The batches in draw order.</summary>
    public ReadOnlySpan<DrawBatch> Batches => CollectionsMarshal.AsSpan(batches);

    public int Count => batches.Count;

    /// <summary>True when nothing but shapes has been placed (so every shape joins batch 0).</summary>
    public bool OnlyShapes => batches.Count == 0 || (batches.Count == 1 && batches[0].Kind == DrawKind.Shape);

    public void Reset()
    {
        batches.Clear();
        boxes.Clear();
        Array.Fill(lastBatchOfKind, -1);
        Array.Fill(kindInOrder, true);
    }

    /// <summary>
    /// Chooses the batch for a draw of <paramref name="kind"/> covering the given device bounds:
    /// after the last batch it overlaps, in the first batch of its own kind from there, else a new
    /// batch at the end. A backdrop blur never joins the overlapping batch itself: it samples a
    /// copy of the framebuffer taken before its batch, so that batch must not contain anything it
    /// should see.
    /// </summary>
    public int Place(DrawKind kind, float minX, float minY, float maxX, float maxY)
    {
        int last = -1;
        for (int i = batches.Count - 1; i >= 0; i--)
        {
            if (Overlaps(i, minX, minY, maxX, maxY))
            {
                last = i;
                break;
            }
        }

        int from = kind == DrawKind.Blur ? last + 1 : Math.Max(last, 0);
        var span = CollectionsMarshal.AsSpan(batches);
        for (int i = from; i < span.Length; i++)
        {
            if (span[i].Kind == kind)
            {
                Grow(i, minX, minY, maxX, maxY);
                return i;
            }
        }

        batches.Add(new DrawBatch { Kind = kind, MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY, BoxCount = 1 });
        CollectionsMarshal.SetCount(boxes, batches.Count * BoxesPerBatch);
        int index = batches.Count - 1;
        boxes[index * BoxesPerBatch] = new BoundsBox(minX, minY, maxX, maxY);
        return index;
    }

    /// <summary>
    /// Records that elements [start, start+count) of a kind's paint-order list went to
    /// <paramref name="batch"/>, extending the previous range in <paramref name="items"/> when it
    /// continues it.
    /// </summary>
    public void Record(DrawKind kind, List<DrawItem> items, int batch, int start, int count)
    {
        AddCount(batch, count);

        int k = (int)kind;
        if (batch < lastBatchOfKind[k])
        {
            kindInOrder[k] = false;
        }
        lastBatchOfKind[k] = batch;

        if (items.Count > 0)
        {
            ref var lastItem = ref CollectionsMarshal.AsSpan(items)[items.Count - 1];
            if (lastItem.Batch == batch && lastItem.Start + lastItem.Count == start)
            {
                lastItem.Count += count;
                return;
            }
        }
        items.Add(new DrawItem { Batch = batch, Start = start, Count = count });
    }

    /// <summary>Adds elements to a batch that has no paint-order list (strip coverage).</summary>
    public void AddCount(int batch, int count)
    {
        CollectionsMarshal.AsSpan(batches)[batch].Count += count;
    }

    /// <summary>Gives each batch its range in its kind's upload buffer (batches of a kind are contiguous, in order).</summary>
    public void AssignStarts()
    {
        Span<int> running = stackalloc int[KindCount];
        foreach (ref var batch in CollectionsMarshal.AsSpan(batches))
        {
            batch.Start = running[(int)batch.Kind];
            running[(int)batch.Kind] += batch.Count;
        }
    }

    /// <summary>
    /// Returns a kind's elements in batch order: <paramref name="source"/> itself when every draw of
    /// the kind landed in non-decreasing batches (the usual case — no copy), else a stable scatter
    /// into <paramref name="ordered"/>. <see cref="AssignStarts"/> must have run.
    /// </summary>
    public List<T> Order<T>(DrawKind kind, List<T> source, List<DrawItem> items, List<T> ordered)
        where T : struct
    {
        if (kindInOrder[(int)kind])
        {
            return source;
        }

        var span = CollectionsMarshal.AsSpan(batches);
        foreach (ref var batch in span)
        {
            batch.Fill = batch.Start;
        }

        ordered.Clear();
        CollectionsMarshal.SetCount(ordered, source.Count);
        var src = CollectionsMarshal.AsSpan(source);
        var dst = CollectionsMarshal.AsSpan(ordered);
        foreach (var item in items)
        {
            ref var batch = ref span[item.Batch];
            src.Slice(item.Start, item.Count).CopyTo(dst.Slice(batch.Fill, item.Count));
            batch.Fill += item.Count;
        }
        return ordered;
    }

    private bool Overlaps(int batch, float minX, float minY, float maxX, float maxY)
    {
        ref readonly var b = ref CollectionsMarshal.AsSpan(batches)[batch];
        if (!(minX < b.MaxX && maxX > b.MinX && minY < b.MaxY && maxY > b.MinY))
        {
            return false;
        }

        foreach (ref readonly var box in CollectionsMarshal.AsSpan(boxes).Slice(batch * BoxesPerBatch, b.BoxCount))
        {
            if (box.Overlaps(minX, minY, maxX, maxY))
            {
                return true;
            }
        }
        return false;
    }

    private void Grow(int batch, float minX, float minY, float maxX, float maxY)
    {
        ref var b = ref CollectionsMarshal.AsSpan(batches)[batch];
        b.MinX = Math.Min(b.MinX, minX);
        b.MinY = Math.Min(b.MinY, minY);
        b.MaxX = Math.Max(b.MaxX, maxX);
        b.MaxY = Math.Max(b.MaxY, maxY);

        var footprint = CollectionsMarshal.AsSpan(boxes).Slice(batch * BoxesPerBatch, BoxesPerBatch);
        for (int i = 0; i < b.BoxCount; i++)
        {
            if (footprint[i].Overlaps(minX, minY, maxX, maxY))
            {
                footprint[i] = footprint[i].Union(minX, minY, maxX, maxY);
                return;
            }
        }

        if (b.BoxCount < BoxesPerBatch)
        {
            footprint[b.BoxCount++] = new BoundsBox(minX, minY, maxX, maxY);
            return;
        }

        int best = 0;
        float bestGrowth = float.MaxValue;
        for (int i = 0; i < BoxesPerBatch; i++)
        {
            float growth = footprint[i].Union(minX, minY, maxX, maxY).Area - footprint[i].Area;
            if (growth < bestGrowth)
            {
                bestGrowth = growth;
                best = i;
            }
        }
        footprint[best] = footprint[best].Union(minX, minY, maxX, maxY);
    }
}

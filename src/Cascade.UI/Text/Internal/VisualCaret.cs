using System;
using System.Collections.Generic;

namespace Cascade.UI;

/// <summary>
/// WP-3531: visual-order caret model for bidirectional text. The layout stores
/// glyphs in <em>visual</em> order with a per-glyph <see cref="GlyphPosition.Rtl"/>
/// flag; this turns that into the set of caret stops on a line ordered left-to-right
/// by screen X, so caret geometry, Left/Right movement and selection can be
/// visual-order-correct instead of assuming logical order equals visual order.
///
/// A caret stop is a (logical offset, line-relative X) pair at a cluster boundary.
/// For an LTR glyph the logical-leading edge is its left (X); for an RTL glyph it is
/// its right (X+advance). The offset past the last logical character (line end) is
/// placed at that character's trailing edge.
/// </summary>
internal static class VisualCaret
{
    /// <summary>
    /// The caret stops on <paramref name="line"/>, ordered left-to-right by
    /// line-relative X. Each distinct cluster offset appears once, plus the line-end
    /// offset. An empty line yields a single stop at its start.
    /// </summary>
    public static List<(int Offset, float X)> Stops(LayoutLine line)
    {
        var glyphs = line.Glyphs;
        int lineStart = line.TextStart;
        int lineEnd = line.TextStart + line.TextLength;

        var stops = new List<(int Offset, float X)>();
        if (glyphs.Count == 0)
        {
            stops.Add((lineStart, 0f));
            return stops;
        }

        // Leading edge of each distinct cluster start (first glyph wins in visual order).
        var seen = new HashSet<int>();
        int maxCluster = int.MinValue;
        GlyphPosition logicalLast = glyphs[0];
        foreach (var g in glyphs)
        {
            if (seen.Add(g.ClusterIndex))
            {
                float leadingX = g.Rtl ? g.X + g.AdvanceWidth : g.X;
                stops.Add((g.ClusterIndex, leadingX));
            }
            if (g.ClusterIndex > maxCluster)
            {
                maxCluster = g.ClusterIndex;
                logicalLast = g;
            }
        }

        // Line-end caret sits at the trailing edge of the logically-last glyph.
        float endX = logicalLast.Rtl ? logicalLast.X : logicalLast.X + logicalLast.AdvanceWidth;
        if (seen.Add(lineEnd))
        {
            stops.Add((lineEnd, endX));
        }

        stops.Sort(static (a, b) => a.X.CompareTo(b.X));
        return stops;
    }

    /// <summary>
    /// Line-relative X of the caret at <paramref name="offset"/> (add
    /// <c>line.X</c> for absolute). Visual-order correct in RTL/mixed text.
    /// </summary>
    public static float XForOffset(LayoutLine line, int offset)
    {
        foreach (var (o, x) in Stops(line))
        {
            if (o == offset)
            {
                return x;
            }
        }
        // Offset not on a cluster boundary (e.g. mid-cluster): fall back to the
        // logical scan, which is exact for LTR and a safe approximation otherwise.
        if (line.Glyphs.Count == 0)
        {
            return 0f;
        }
        foreach (var g in line.Glyphs)
        {
            if (g.ClusterIndex >= offset)
            {
                return g.Rtl ? g.X + g.AdvanceWidth : g.X;
            }
        }
        var last = line.Glyphs[^1];
        return last.Rtl ? last.X : last.X + last.AdvanceWidth;
    }

    /// <summary>
    /// The offset of the caret stop immediately to the visual left
    /// (<paramref name="left"/> = true) or right of <paramref name="offset"/> on
    /// this line, or -1 if <paramref name="offset"/> is at that visual edge of the
    /// line (the caller then crosses to an adjacent line).
    /// </summary>
    public static int VisualNeighbor(LayoutLine line, int offset, bool left)
    {
        var stops = Stops(line);
        int idx = -1;
        for (int i = 0; i < stops.Count; i++)
        {
            if (stops[i].Offset == offset)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0)
        {
            return -1;
        }
        int n = left ? idx - 1 : idx + 1;
        return n >= 0 && n < stops.Count ? stops[n].Offset : -1;
    }

    /// <summary>
    /// The writing direction of the run the caret at <paramref name="offset"/> sits
    /// in — the direction of the glyph whose cluster starts at that offset (or the
    /// preceding glyph at line end).
    /// </summary>
    public static TextDirection DirectionAt(LayoutLine line, int offset)
    {
        GlyphPosition? match = null;
        GlyphPosition? prev = null;
        foreach (var g in line.Glyphs)
        {
            if (g.ClusterIndex == offset)
            {
                match = g;
                break;
            }
            if (g.ClusterIndex < offset)
            {
                prev = g;
            }
        }
        var chosen = match ?? prev;
        return chosen is { } gp && gp.Rtl ? TextDirection.RightToLeft : TextDirection.LeftToRight;
    }
}

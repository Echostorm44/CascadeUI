using System;
using System.Collections.Generic;

namespace Cascade.UI;

/// <summary>
/// Rasterizes a stroked vector icon into an anti-aliased RGBA8 bitmap on the CPU —
/// the same idea browsers and Flutter use for icon glyphs: render once at the target
/// device-pixel size, cache, then blit. Anti-aliasing is analytic: each pixel's alpha
/// is the coverage of the stroke, computed from the signed distance to the flattened
/// path polyline (the same smoothstep/coverage model Etch's GPU shaders use), so curves
/// and straight strokes alike come out smooth with no jaggies, lumps, or dropped
/// subpaths.
/// </summary>
internal static class IconRasterizer
{
    /// <summary>
    /// The margin, in device pixels, a bitmap needs around the icon's box so a stroke on the
    /// view-box edge (half the stroke outside it) and its 1px anti-aliasing fringe are not cut.
    /// </summary>
    public static int MarginFor(float strokeWidthPx)
    {
        return (int)MathF.Ceiling((strokeWidthPx * 0.5f) + 1f);
    }

    /// <summary>
    /// Rasterizes <paramref name="paths"/> (view-box <paramref name="viewW"/>×<paramref name="viewH"/>),
    /// stroked at <paramref name="strokeWidthPx"/> and tinted with <paramref name="color"/>. The
    /// view box is aspect-fit into a <paramref name="boxPx"/>² box — the icon's size, as in SVG —
    /// and the bitmap adds <paramref name="marginPx"/> on every side of that box, so it is
    /// (<paramref name="boxPx"/> + 2 × <paramref name="marginPx"/>)² straight-alpha RGBA8 pixels.
    /// </summary>
    /// <remarks>
    /// The margin used to be an inset: the art was shrunk into box − 2 × (stroke/2 + 1.5) so a
    /// stroke on the edge stayed inside. Icon sets already keep their art off the edge (Lucide's
    /// 24-unit box has a 2-unit margin), so that was a second shrink, worst at small sizes: a 14px
    /// icon drew its 24-unit box in 9px. The bitmap now grows around the box instead, and the
    /// caller blits it correspondingly larger, so the art fills its box at every size.
    /// </remarks>
    public static byte[] Rasterize(
        ReadOnlySpan<string> paths, float viewW, float viewH,
        int boxPx, float strokeWidthPx, ColorValue color, int marginPx, bool fill = false)
    {
        int pxSize = boxPx + (2 * Math.Max(0, marginPx));
        var rgba = new byte[pxSize * pxSize * 4];
        if (viewW <= 0 || viewH <= 0 || boxPx <= 0)
        {
            return rgba;
        }

        // View-box → pixel space: aspect-fit into the box, centered in the bitmap (inside the margin).
        float scale = MathF.Min(boxPx / viewW, boxPx / viewH);
        float offX = (pxSize - viewW * scale) * 0.5f;
        float offY = (pxSize - viewH * scale) * 0.5f;

        int curveSegments = Math.Clamp((int)MathF.Ceiling(scale * 24f), 12, 64);

        // Flatten every subpath to line segments in pixel space (4 floats per segment). For a filled
        // icon each subpath is also closed back to its start, into the edges the fill is wound from.
        var seg = new List<float>(256);
        var edges = fill ? new List<float>(256) : null;
        float cx = 0, cy = 0, startX = 0, startY = 0;
        bool have = false;
        void CloseSubpath()
        {
            if (edges is not null && have && (cx != startX || cy != startY))
            {
                edges.Add(cx); edges.Add(cy); edges.Add(startX); edges.Add(startY);
            }
        }
        foreach (var p in paths)
        {
            have = false;
            SvgPathFlattener.Flatten(
                p, curveSegments,
                moveTo: (x, y) =>
                {
                    CloseSubpath();
                    cx = startX = offX + x * scale; cy = startY = offY + y * scale; have = true;
                },
                lineTo: (x, y) =>
                {
                    float nx = offX + x * scale, ny = offY + y * scale;
                    if (have)
                    {
                        seg.Add(cx); seg.Add(cy); seg.Add(nx); seg.Add(ny);
                        edges?.Add(cx); edges?.Add(cy); edges?.Add(nx); edges?.Add(ny);
                    }
                    cx = nx; cy = ny;
                    have = true;
                });
            CloseSubpath();
        }

        int segCount = seg.Count / 4;
        if (segCount == 0)
        {
            return rgba;
        }

        // The bitmap is straight RGBA8 and treated as sRGB, so the tint goes in sRGB-encoded, not as
        // ColorValue's internal linear values; alpha carries the coverage.
        float a = color.A;
        (byte cr, byte cg, byte cb) = color.ToSrgbBytes();

        float half = strokeWidthPx * 0.5f;
        // The coverage ramp spans ~1px across the stroke edge (a box-filter approximation).
        float lo = half - 0.5f;

        var s = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(seg);
        var e = edges is null ? default : System.Runtime.InteropServices.CollectionsMarshal.AsSpan(edges);
        for (int y = 0; y < pxSize; y++)
        {
            float py = y + 0.5f;
            int row = y * pxSize * 4;
            for (int x = 0; x < pxSize; x++)
            {
                float px = x + 0.5f;
                float best = float.MaxValue;
                for (int k = 0; k < s.Length; k += 4)
                {
                    float d = DistToSegment(px, py, s[k], s[k + 1], s[k + 2], s[k + 3]);
                    if (d < best)
                    {
                        best = d;
                        if (best <= lo)
                        {
                            break; // fully inside the stroke — can't get more covered
                        }
                    }
                }

                float cov = Math.Clamp(lo + 1f - best, 0f, 1f); // 1px linear AA band
                if (cov < 1f && e.Length > 0 && Winding(e, px, py) != 0)
                {
                    cov = 1f; // inside a filled shape; the stroke over its outline gives the edge its AA
                }
                if (cov <= 0f)
                {
                    continue;
                }

                int o = row + x * 4;
                rgba[o] = cr;
                rgba[o + 1] = cg;
                rgba[o + 2] = cb;
                rgba[o + 3] = ToByte(a * cov);
            }
        }

        return rgba;
    }

    // Non-zero winding number of (px, py) against closed edges (4 floats each).
    private static int Winding(ReadOnlySpan<float> e, float px, float py)
    {
        int winding = 0;
        for (int k = 0; k < e.Length; k += 4)
        {
            float ax = e[k], ay = e[k + 1], bx = e[k + 2], by = e[k + 3];
            if (ay <= py)
            {
                if (by > py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) > 0f)
                {
                    winding++;
                }
            }
            else if (by <= py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) < 0f)
            {
                winding--;
            }
        }
        return winding;
    }

    private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float len2 = dx * dx + dy * dy;
        float t = len2 > 0f ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0f;
        t = Math.Clamp(t, 0f, 1f);
        float qx = ax + t * dx, qy = ay + t * dy;
        float ex = px - qx, ey = py - qy;
        return MathF.Sqrt(ex * ex + ey * ey);
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}

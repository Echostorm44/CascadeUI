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
    /// Rasterizes <paramref name="paths"/> (view-box <paramref name="viewW"/>×<paramref name="viewH"/>)
    /// into a straight-alpha RGBA8 bitmap of <paramref name="pxSize"/>² pixels, stroked at
    /// <paramref name="strokeWidthPx"/> and tinted with <paramref name="color"/>. The art is
    /// aspect-fit and centered with <paramref name="paddingPx"/> of inset on every edge.
    /// </summary>
    public static byte[] Rasterize(
        ReadOnlySpan<string> paths, float viewW, float viewH,
        int pxSize, float strokeWidthPx, ColorValue color, float paddingPx)
    {
        var rgba = new byte[pxSize * pxSize * 4];
        if (viewW <= 0 || viewH <= 0 || pxSize <= 0)
        {
            return rgba;
        }

        // View-box → pixel space: aspect-fit, centered, inset by padding.
        float avail = MathF.Max(1f, pxSize - 2f * paddingPx);
        float scale = MathF.Min(avail / viewW, avail / viewH);
        float offX = (pxSize - viewW * scale) * 0.5f;
        float offY = (pxSize - viewH * scale) * 0.5f;

        int curveSegments = Math.Clamp((int)MathF.Ceiling(scale * 24f), 12, 64);

        // Flatten every subpath to line segments in pixel space (4 floats per segment).
        var seg = new List<float>(256);
        float cx = 0, cy = 0;
        bool have = false;
        foreach (var p in paths)
        {
            have = false;
            SvgPathFlattener.Flatten(
                p, curveSegments,
                moveTo: (x, y) => { cx = offX + x * scale; cy = offY + y * scale; have = true; },
                lineTo: (x, y) =>
                {
                    float nx = offX + x * scale, ny = offY + y * scale;
                    if (have)
                    {
                        seg.Add(cx); seg.Add(cy); seg.Add(nx); seg.Add(ny);
                    }
                    cx = nx; cy = ny;
                    have = true;
                });
        }

        int segCount = seg.Count / 4;
        if (segCount == 0)
        {
            return rgba;
        }

        // Un-premultiply the tint to straight RGB; alpha carries the coverage.
        float a = color.A;
        float inv = a > 1e-4f ? 1f / a : 0f;
        byte cr = ToByte(color.R * inv);
        byte cg = ToByte(color.G * inv);
        byte cb = ToByte(color.B * inv);

        float half = strokeWidthPx * 0.5f;
        // The coverage ramp spans ~1px across the stroke edge (a box-filter approximation).
        float lo = half - 0.5f;

        var s = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(seg);
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

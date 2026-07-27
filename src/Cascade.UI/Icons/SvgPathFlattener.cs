using System;

namespace Cascade.UI;

/// <summary>
/// Parses SVG path data and flattens it into straight-line segments, reporting each
/// subpath as a <c>moveTo</c> followed by <c>lineTo</c> calls in the path's own
/// (view-box) coordinate space. Curves (cubic/quadratic Béziers, elliptical arcs) are
/// tessellated to <paramref name="curveSegments"/> chords. The caller applies any
/// transform and decides what to do with the points (rasterize, stroke, hit-test).
/// </summary>
/// <remarks>
/// Supports the full path grammar used by icon art: M/L/H/V, C/S, Q/T, A, Z, both
/// absolute and relative forms, and implicit command repetition (a bare coordinate
/// after a moveto is an implicit lineto).
/// </remarks>
internal static class SvgPathFlattener
{
    internal delegate void CubicEmitter(
        float x0, float y0, float c1x, float c1y, float c2x, float c2y, float x1, float y1);

    /// <summary>
    /// Flattens <paramref name="pathData"/>. <paramref name="moveTo"/> starts a new
    /// subpath; <paramref name="lineTo"/> adds a segment from the current point.
    /// </summary>
    public static void Flatten(
        string pathData, int curveSegments,
        Action<float, float> moveTo, Action<float, float> lineTo)
    {
        if (string.IsNullOrEmpty(pathData))
        {
            return;
        }

        int segs = Math.Clamp(curveSegments, 1, 256);

        void Cubic(float x0, float y0, float c1x, float c1y, float c2x, float c2y, float x1, float y1)
        {
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs, mt = 1f - t;
                float a = mt * mt * mt, b = 3f * mt * mt * t, c = 3f * mt * t * t, d = t * t * t;
                lineTo(a * x0 + b * c1x + c * c2x + d * x1, a * y0 + b * c1y + c * c2y + d * y1);
            }
        }

        void Quad(float x0, float y0, float cx, float cy, float x1, float y1)
        {
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs, mt = 1f - t;
                lineTo(mt * mt * x0 + 2f * mt * t * cx + t * t * x1,
                       mt * mt * y0 + 2f * mt * t * cy + t * t * y1);
            }
        }

        float curX = 0, curY = 0;
        float startX = 0, startY = 0;
        float prevCx = 0, prevCy = 0;
        float prevQx = 0, prevQy = 0;
        char prevCmd = ' ', cmd = ' ';
        int i = 0;

        while (i < pathData.Length)
        {
            char ch = pathData[i];
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                i++;
                continue;
            }

            if (char.IsLetter(ch))
            {
                cmd = ch;
                i++;
            }
            else
            {
                // A bare number repeats the previous command; after a moveto the
                // implicit repeat is a lineto.
                if (cmd == 'M') { cmd = 'L'; }
                else if (cmd == 'm') { cmd = 'l'; }
                else if (cmd == ' ') { i++; continue; }
            }

            bool rel = char.IsLower(cmd);
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                {
                    (float x, float y, i) = ParsePoint(pathData, i);
                    curX = rel ? curX + x : x;
                    curY = rel ? curY + y : y;
                    startX = curX; startY = curY;
                    moveTo(curX, curY);
                    break;
                }
                case 'L':
                {
                    (float x, float y, i) = ParsePoint(pathData, i);
                    curX = rel ? curX + x : x;
                    curY = rel ? curY + y : y;
                    lineTo(curX, curY);
                    break;
                }
                case 'H':
                {
                    (float v, i) = ParseNumber(pathData, i);
                    curX = rel ? curX + v : v;
                    lineTo(curX, curY);
                    break;
                }
                case 'V':
                {
                    (float v, i) = ParseNumber(pathData, i);
                    curY = rel ? curY + v : v;
                    lineTo(curX, curY);
                    break;
                }
                case 'C':
                {
                    (float c1x, float c1y, i) = ParsePoint(pathData, i);
                    (float c2x, float c2y, i) = ParsePoint(pathData, i);
                    (float ex, float ey, i) = ParsePoint(pathData, i);
                    if (rel) { c1x += curX; c1y += curY; c2x += curX; c2y += curY; ex += curX; ey += curY; }
                    Cubic(curX, curY, c1x, c1y, c2x, c2y, ex, ey);
                    prevCx = c2x; prevCy = c2y;
                    curX = ex; curY = ey;
                    break;
                }
                case 'S':
                {
                    (float c2x, float c2y, i) = ParsePoint(pathData, i);
                    (float ex, float ey, i) = ParsePoint(pathData, i);
                    if (rel) { c2x += curX; c2y += curY; ex += curX; ey += curY; }
                    char pu = char.ToUpperInvariant(prevCmd);
                    float c1x = (pu == 'C' || pu == 'S') ? 2f * curX - prevCx : curX;
                    float c1y = (pu == 'C' || pu == 'S') ? 2f * curY - prevCy : curY;
                    Cubic(curX, curY, c1x, c1y, c2x, c2y, ex, ey);
                    prevCx = c2x; prevCy = c2y;
                    curX = ex; curY = ey;
                    break;
                }
                case 'Q':
                {
                    (float cx, float cy, i) = ParsePoint(pathData, i);
                    (float ex, float ey, i) = ParsePoint(pathData, i);
                    if (rel) { cx += curX; cy += curY; ex += curX; ey += curY; }
                    Quad(curX, curY, cx, cy, ex, ey);
                    prevQx = cx; prevQy = cy;
                    curX = ex; curY = ey;
                    break;
                }
                case 'T':
                {
                    (float ex, float ey, i) = ParsePoint(pathData, i);
                    if (rel) { ex += curX; ey += curY; }
                    char pu = char.ToUpperInvariant(prevCmd);
                    float cx = (pu == 'Q' || pu == 'T') ? 2f * curX - prevQx : curX;
                    float cy = (pu == 'Q' || pu == 'T') ? 2f * curY - prevQy : curY;
                    Quad(curX, curY, cx, cy, ex, ey);
                    prevQx = cx; prevQy = cy;
                    curX = ex; curY = ey;
                    break;
                }
                case 'A':
                {
                    (float rx, i) = ParseNumber(pathData, i);
                    (float ry, i) = ParseNumber(pathData, i);
                    (float rot, i) = ParseNumber(pathData, i);
                    (float laf, i) = ParseFlag(pathData, i);
                    (float sf, i) = ParseFlag(pathData, i);
                    (float ex, float ey, i) = ParsePoint(pathData, i);
                    if (rel) { ex += curX; ey += curY; }
                    AppendArc(Cubic, curX, curY, rx, ry, rot, laf != 0f, sf != 0f, ex, ey);
                    curX = ex; curY = ey;
                    break;
                }
                case 'Z':
                {
                    lineTo(startX, startY);
                    curX = startX; curY = startY;
                    break;
                }
                default:
                    i++; // unknown command — skip
                    break;
            }

            prevCmd = cmd;
        }
    }

    // Emits an SVG elliptical arc (from (x1,y1) to (x2,y2)) as cubic Béziers via
    // <paramref name="emit"/>, following the SVG implementation notes (endpoint →
    // center parameterization, split into ≤90° segments).
    private static void AppendArc(
        CubicEmitter emit,
        float x1, float y1, float rx, float ry, float phiDeg,
        bool largeArc, bool sweep, float x2, float y2)
    {
        if (rx == 0f || ry == 0f || (x1 == x2 && y1 == y2))
        {
            emit(x1, y1, x1, y1, x2, y2, x2, y2);
            return;
        }

        rx = MathF.Abs(rx);
        ry = MathF.Abs(ry);
        float phi = phiDeg * MathF.PI / 180f;
        float cosPhi = MathF.Cos(phi);
        float sinPhi = MathF.Sin(phi);

        float dx = (x1 - x2) / 2f;
        float dy = (y1 - y2) / 2f;
        float x1p = cosPhi * dx + sinPhi * dy;
        float y1p = -sinPhi * dx + cosPhi * dy;

        float lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1f)
        {
            float s = MathF.Sqrt(lambda);
            rx *= s;
            ry *= s;
        }

        float rx2 = rx * rx, ry2 = ry * ry;
        float x1p2 = x1p * x1p, y1p2 = y1p * y1p;
        float num = rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2;
        float den = rx2 * y1p2 + ry2 * x1p2;
        float factor = den == 0f ? 0f : MathF.Sqrt(MathF.Max(0f, num / den));
        if (largeArc == sweep)
        {
            factor = -factor;
        }
        float cxp = factor * rx * y1p / ry;
        float cyp = factor * -ry * x1p / rx;

        float cx = cosPhi * cxp - sinPhi * cyp + (x1 + x2) / 2f;
        float cy = sinPhi * cxp + cosPhi * cyp + (y1 + y2) / 2f;

        float theta1 = ArcAngle(1f, 0f, (x1p - cxp) / rx, (y1p - cyp) / ry);
        float dtheta = ArcAngle((x1p - cxp) / rx, (y1p - cyp) / ry,
            (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && dtheta > 0f)
        {
            dtheta -= 2f * MathF.PI;
        }
        else if (sweep && dtheta < 0f)
        {
            dtheta += 2f * MathF.PI;
        }

        int segments = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(dtheta) / (MathF.PI / 2f)));
        float delta = dtheta / segments;
        float t = 4f / 3f * MathF.Tan(delta / 4f);

        for (int s = 0; s < segments; s++)
        {
            float a0 = theta1 + s * delta;
            float a1 = a0 + delta;
            float cos0 = MathF.Cos(a0), sin0 = MathF.Sin(a0);
            float cos1 = MathF.Cos(a1), sin1 = MathF.Sin(a1);

            float sx = cx + rx * cosPhi * cos0 - ry * sinPhi * sin0;
            float sy = cy + rx * sinPhi * cos0 + ry * cosPhi * sin0;
            float ex = cx + rx * cosPhi * cos1 - ry * sinPhi * sin1;
            float ey = cy + rx * sinPhi * cos1 + ry * cosPhi * sin1;

            float dsx = -rx * cosPhi * sin0 - ry * sinPhi * cos0;
            float dsy = -rx * sinPhi * sin0 + ry * cosPhi * cos0;
            float dex = -rx * cosPhi * sin1 - ry * sinPhi * cos1;
            float dey = -rx * sinPhi * sin1 + ry * cosPhi * cos1;

            emit(sx, sy, sx + t * dsx, sy + t * dsy, ex - t * dex, ey - t * dey, ex, ey);
        }
    }

    private static float ArcAngle(float ux, float uy, float vx, float vy)
    {
        float dot = ux * vx + uy * vy;
        float len = MathF.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        float ang = len == 0f ? 0f : MathF.Acos(Math.Clamp(dot / len, -1f, 1f));
        if (ux * vy - uy * vx < 0f)
        {
            ang = -ang;
        }
        return ang;
    }

    // Parses a single SVG arc flag (0 or 1), which may appear un-delimited from the
    // following number (e.g. "a5 5 0 015 5"). Falls back to a normal number.
    private static (float value, int nextIndex) ParseFlag(string data, int index)
    {
        while (index < data.Length && (char.IsWhiteSpace(data[index]) || data[index] == ','))
        {
            index++;
        }
        if (index < data.Length && (data[index] == '0' || data[index] == '1'))
        {
            return (data[index] == '1' ? 1f : 0f, index + 1);
        }
        return ParseNumber(data, index);
    }

    private static (float value, int nextIndex) ParseNumber(string data, int index)
    {
        while (index < data.Length && (char.IsWhiteSpace(data[index]) || data[index] == ','))
        {
            index++;
        }

        int start = index;
        if (index < data.Length && (data[index] == '-' || data[index] == '+'))
        {
            index++;
        }

        // Mantissa: digits with at most one decimal point. A second '.' begins the
        // next number (SVG shorthand: "-.43.25" is two numbers, -0.43 and 0.25), so
        // it terminates this one rather than being swallowed into an invalid token.
        bool seenDot = false;
        while (index < data.Length)
        {
            char c = data[index];
            if (char.IsDigit(c))
            {
                index++;
            }
            else if (c == '.' && !seenDot)
            {
                seenDot = true;
                index++;
            }
            else
            {
                break;
            }
        }

        // Optional exponent: e/E, an optional sign, then digits.
        if (index < data.Length && (data[index] == 'e' || data[index] == 'E'))
        {
            int expStart = index;
            index++;
            if (index < data.Length && (data[index] == '-' || data[index] == '+'))
            {
                index++;
            }
            if (index < data.Length && char.IsDigit(data[index]))
            {
                while (index < data.Length && char.IsDigit(data[index]))
                {
                    index++;
                }
            }
            else
            {
                // Not actually an exponent (e.g. a following 'e' command) — rewind.
                index = expStart;
            }
        }

        float value = float.Parse(data.AsSpan(start, index - start),
            System.Globalization.CultureInfo.InvariantCulture);
        return (value, index);
    }

    private static (float x, float y, int nextIndex) ParsePoint(string data, int index)
    {
        (float x, index) = ParseNumber(data, index);
        (float y, index) = ParseNumber(data, index);
        return (x, y, index);
    }
}

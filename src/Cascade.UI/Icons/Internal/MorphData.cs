using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cascade.UI;

internal readonly record struct PointF(float X, float Y);

internal enum PathCommand : byte
{
    MoveTo,
    LineTo,
    CubicTo,
    QuadTo,
    Close,
}

internal readonly struct MorphSegment
{
    internal readonly PointF[] FromPoints;
    internal readonly PointF[] ToPoints;
    internal readonly PathCommand Command;

    internal MorphSegment(PathCommand command, PointF[] fromPoints, PointF[] toPoints)
    {
        Command = command;
        FromPoints = fromPoints;
        ToPoints = toPoints;
    }

    internal PointF[] Interpolate(float t)
    {
        var result = new PointF[FromPoints.Length];
        for (int i = 0; i < FromPoints.Length; i++)
        {
            result[i] = new PointF(
                FromPoints[i].X + (ToPoints[i].X - FromPoints[i].X) * t,
                FromPoints[i].Y + (ToPoints[i].Y - FromPoints[i].Y) * t
            );
        }
        return result;
    }
}

/// <summary>
/// Holds normalized path data for morph transitions between two icon shapes.
/// Pre-processes path segments so they can be linearly interpolated.
/// </summary>
internal sealed class MorphData
{
    internal MorphSegment[] Segments { get; }
    internal bool IsCompatible { get; }

    internal MorphData(ReadOnlySpan<string> fromPaths, ReadOnlySpan<string> toPaths)
    {
        if (fromPaths.Length == 0 && toPaths.Length == 0)
        {
            Segments = [];
            IsCompatible = true;
            return;
        }

        if (fromPaths.Length == 0 || toPaths.Length == 0)
        {
            Segments = [];
            IsCompatible = false;
            return;
        }

        var fromSegments = new List<PathSegment>();
        var toSegments = new List<PathSegment>();

        foreach (var path in fromPaths)
        {
            fromSegments.AddRange(SvgPathParser.Parse(path));
        }

        foreach (var path in toPaths)
        {
            toSegments.AddRange(SvgPathParser.Parse(path));
        }

        if (fromSegments.Count == 0 && toSegments.Count == 0)
        {
            Segments = [];
            IsCompatible = true;
            return;
        }

        if (fromSegments.Count == 0 || toSegments.Count == 0)
        {
            Segments = [];
            IsCompatible = false;
            return;
        }

        var (normalizedFrom, normalizedTo) = PathNormalizer.Normalize(
            [.. fromSegments], [.. toSegments]);

        Segments = BuildMorphSegments(normalizedFrom, normalizedTo);
        IsCompatible = true;
    }

    private static MorphSegment[] BuildMorphSegments(MorphSegment[] from, MorphSegment[] to)
    {
        var result = new MorphSegment[from.Length];
        for (int i = 0; i < from.Length; i++)
        {
            result[i] = new MorphSegment(from[i].Command, from[i].FromPoints, to[i].FromPoints);
        }
        return result;
    }

    /// <summary>
    /// Interpolates between from/to at the given t (0..1).
    /// Returns interpolated SVG path data strings.
    /// </summary>
    internal string[] Interpolate(float t)
    {
        if (Segments.Length == 0)
        {
            return [];
        }

        var sb = new StringBuilder();
        bool first = true;
        string? currentPath = null;
        var paths = new List<string>();

        foreach (var seg in Segments)
        {
            if (seg.Command == PathCommand.MoveTo && !first)
            {
                if (currentPath != null)
                {
                    paths.Add(sb.ToString());
                    sb.Clear();
                }
            }

            AppendSegment(sb, seg, t);
            first = false;
            currentPath = sb.ToString();
        }

        if (sb.Length > 0)
        {
            paths.Add(sb.ToString().Trim());
        }

        return [.. paths];
    }

    private static void AppendSegment(StringBuilder sb, MorphSegment seg, float t)
    {
        var pts = seg.Interpolate(t);

        switch (seg.Command)
        {
            case PathCommand.MoveTo:
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append('M');
                AppendPoint(sb, pts[0]);
                break;
            }
            case PathCommand.LineTo:
            {
                sb.Append(' ');
                sb.Append('L');
                AppendPoint(sb, pts[0]);
                break;
            }
            case PathCommand.CubicTo:
            {
                sb.Append(' ');
                sb.Append('C');
                AppendPoint(sb, pts[0]);
                sb.Append(' ');
                AppendPoint(sb, pts[1]);
                sb.Append(' ');
                AppendPoint(sb, pts[2]);
                break;
            }
            case PathCommand.QuadTo:
            {
                sb.Append(' ');
                sb.Append('Q');
                AppendPoint(sb, pts[0]);
                sb.Append(' ');
                AppendPoint(sb, pts[1]);
                break;
            }
            case PathCommand.Close:
            {
                sb.Append(" Z");
                break;
            }
        }
    }

    private static void AppendPoint(StringBuilder sb, PointF pt)
    {
        sb.Append(pt.X.ToString("G6", CultureInfo.InvariantCulture));
        sb.Append(' ');
        sb.Append(pt.Y.ToString("G6", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Parses SVG path data strings into PathSegment arrays.
/// Supports M, L, C, Q, Z commands (absolute and relative).
/// </summary>
internal static class SvgPathParser
{
    internal static List<PathSegment> Parse(string pathData)
    {
        var segments = new List<PathSegment>();
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return segments;
        }

        var tokens = Tokenize(pathData);
        int idx = 0;
        PointF current = new(0f, 0f);
        PointF start = new(0f, 0f);

        while (idx < tokens.Count)
        {
            string token = tokens[idx];
            if (token.Length != 1 || !char.IsLetter(token[0]))
            {
                idx++;
                continue;
            }

            char cmd = token[0];
            idx++;

            switch (cmd)
            {
                case 'M':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float x = ParseFloat(tokens[idx++]);
                        float y = ParseFloat(tokens[idx++]);
                        current = new PointF(x, y);
                        start = current;
                        segments.Add(new PathSegment(PathCommand.MoveTo, [current]));
                        // Subsequent coordinate pairs treated as LineTo
                        cmd = 'L';
                    }
                    break;
                }
                case 'm':
                {
                    bool firstMove = true;
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float dx = ParseFloat(tokens[idx++]);
                        float dy = ParseFloat(tokens[idx++]);
                        current = new PointF(current.X + dx, current.Y + dy);
                        if (firstMove)
                        {
                            start = current;
                            firstMove = false;
                        }
                        segments.Add(new PathSegment(PathCommand.MoveTo, [current]));
                        // Subsequent coordinate pairs treated as relative lineto
                        cmd = 'l';
                    }
                    break;
                }
                case 'L':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float x = ParseFloat(tokens[idx++]);
                        float y = ParseFloat(tokens[idx++]);
                        current = new PointF(x, y);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'l':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float dx = ParseFloat(tokens[idx++]);
                        float dy = ParseFloat(tokens[idx++]);
                        current = new PointF(current.X + dx, current.Y + dy);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'H':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float x = ParseFloat(tokens[idx++]);
                        current = new PointF(x, current.Y);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'h':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float dx = ParseFloat(tokens[idx++]);
                        current = new PointF(current.X + dx, current.Y);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'V':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float y = ParseFloat(tokens[idx++]);
                        current = new PointF(current.X, y);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'v':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float dy = ParseFloat(tokens[idx++]);
                        current = new PointF(current.X, current.Y + dy);
                        segments.Add(new PathSegment(PathCommand.LineTo, [current]));
                    }
                    break;
                }
                case 'C':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float cx1 = ParseFloat(tokens[idx++]);
                        float cy1 = ParseFloat(tokens[idx++]);
                        float cx2 = ParseFloat(tokens[idx++]);
                        float cy2 = ParseFloat(tokens[idx++]);
                        float x = ParseFloat(tokens[idx++]);
                        float y = ParseFloat(tokens[idx++]);
                        current = new PointF(x, y);
                        segments.Add(new PathSegment(PathCommand.CubicTo, [
                            new PointF(cx1, cy1),
                            new PointF(cx2, cy2),
                            current
                        ]));
                    }
                    break;
                }
                case 'c':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float cx1 = ParseFloat(tokens[idx++]);
                        float cy1 = ParseFloat(tokens[idx++]);
                        float cx2 = ParseFloat(tokens[idx++]);
                        float cy2 = ParseFloat(tokens[idx++]);
                        float dx = ParseFloat(tokens[idx++]);
                        float dy = ParseFloat(tokens[idx++]);
                        var cp1 = new PointF(current.X + cx1, current.Y + cy1);
                        var cp2 = new PointF(current.X + cx2, current.Y + cy2);
                        current = new PointF(current.X + dx, current.Y + dy);
                        segments.Add(new PathSegment(PathCommand.CubicTo, [cp1, cp2, current]));
                    }
                    break;
                }
                case 'Q':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float cx = ParseFloat(tokens[idx++]);
                        float cy = ParseFloat(tokens[idx++]);
                        float x = ParseFloat(tokens[idx++]);
                        float y = ParseFloat(tokens[idx++]);
                        current = new PointF(x, y);
                        segments.Add(new PathSegment(PathCommand.QuadTo, [
                            new PointF(cx, cy),
                            current
                        ]));
                    }
                    break;
                }
                case 'q':
                {
                    while (idx < tokens.Count && IsNumber(tokens[idx]))
                    {
                        float cx = ParseFloat(tokens[idx++]);
                        float cy = ParseFloat(tokens[idx++]);
                        float dx = ParseFloat(tokens[idx++]);
                        float dy = ParseFloat(tokens[idx++]);
                        var cp = new PointF(current.X + cx, current.Y + cy);
                        current = new PointF(current.X + dx, current.Y + dy);
                        segments.Add(new PathSegment(PathCommand.QuadTo, [cp, current]));
                    }
                    break;
                }
                case 'Z':
                case 'z':
                {
                    segments.Add(new PathSegment(PathCommand.Close, []));
                    current = start;
                    break;
                }
            }
        }

        return segments;
    }

    private static List<string> Tokenize(string pathData)
    {
        var tokens = new List<string>();
        int i = 0;
        int len = pathData.Length;

        while (i < len)
        {
            char c = pathData[i];

            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            if (char.IsLetter(c))
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // Number (including leading minus/plus and scientific notation)
            int start = i;
            if (c == '-' || c == '+')
            {
                i++;
            }

            while (i < len && (char.IsDigit(pathData[i]) || pathData[i] == '.' || pathData[i] == 'e' || pathData[i] == 'E'))
            {
                if ((pathData[i] == 'e' || pathData[i] == 'E') && i + 1 < len && (pathData[i + 1] == '-' || pathData[i + 1] == '+'))
                {
                    i += 2;
                    continue;
                }
                i++;
            }

            if (i > start)
            {
                tokens.Add(pathData[start..i]);
            }
            else
            {
                i++;
            }
        }

        return tokens;
    }

    private static bool IsNumber(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        char first = token[0];
        return char.IsDigit(first) || first == '-' || first == '+' || first == '.';
    }

    private static float ParseFloat(string token)
    {
        return float.Parse(token, CultureInfo.InvariantCulture);
    }
}

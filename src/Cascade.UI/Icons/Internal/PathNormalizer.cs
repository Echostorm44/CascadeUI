using System;
using System.Collections.Generic;

namespace Cascade.UI;

internal readonly struct PathSegment
{
    internal readonly PathCommand Command;
    internal readonly PointF[] Points;

    internal PathSegment(PathCommand command, PointF[] points)
    {
        Command = command;
        Points = points;
    }
}

/// <summary>
/// Normalizes two path segment lists to have equal point counts per segment.
/// This enables smooth morphing between different icon shapes.
/// </summary>
internal static class PathNormalizer
{
    /// <summary>
    /// Takes two lists of parsed path segments and returns normalized MorphSegment arrays
    /// where each corresponding segment pair has the same number of points.
    /// </summary>
    internal static (MorphSegment[] from, MorphSegment[] to) Normalize(
        PathSegment[] fromSegments, PathSegment[] toSegments)
    {
        // Equalize segment counts by padding the shorter list with degenerate segments.
        int count = Math.Max(fromSegments.Length, toSegments.Length);

        var paddedFrom = PadSegments(fromSegments, count);
        var paddedTo = PadSegments(toSegments, count);

        var resultFrom = new MorphSegment[count];
        var resultTo = new MorphSegment[count];

        for (int i = 0; i < count; i++)
        {
            var (f, t) = NormalizeSegmentPair(paddedFrom[i], paddedTo[i]);
            resultFrom[i] = f;
            resultTo[i] = t;
        }

        return (resultFrom, resultTo);
    }

    private static PathSegment[] PadSegments(PathSegment[] segments, int targetCount)
    {
        if (segments.Length >= targetCount)
        {
            return segments;
        }

        var result = new PathSegment[targetCount];
        Array.Copy(segments, result, segments.Length);

        // Last known point for degenerate segments
        PointF lastPoint = segments.Length > 0
            ? GetLastPoint(segments[^1])
            : new PointF(0f, 0f);

        for (int i = segments.Length; i < targetCount; i++)
        {
            // Use the command type from the target side's segment if possible,
            // but create a zero-length degenerate segment at the last point.
            result[i] = MakeDegenerateSegment(PathCommand.LineTo, lastPoint);
        }

        return result;
    }

    private static PointF GetLastPoint(PathSegment seg)
    {
        if (seg.Points.Length == 0)
        {
            return new PointF(0f, 0f);
        }
        return seg.Points[^1];
    }

    private static PathSegment MakeDegenerateSegment(PathCommand command, PointF at)
    {
        return command switch
        {
            PathCommand.MoveTo => new PathSegment(PathCommand.MoveTo, [at]),
            PathCommand.Close => new PathSegment(PathCommand.Close, []),
            PathCommand.CubicTo => new PathSegment(PathCommand.CubicTo, [at, at, at]),
            PathCommand.QuadTo => new PathSegment(PathCommand.QuadTo, [at, at]),
            _ => new PathSegment(PathCommand.LineTo, [at]),
        };
    }

    /// <summary>
    /// Normalizes a single segment pair so both have the same command and point count.
    /// Promotes simpler commands to more complex ones (L → C) as needed.
    /// </summary>
    private static (MorphSegment from, MorphSegment to) NormalizeSegmentPair(
        PathSegment from, PathSegment to)
    {
        // Promote both to the higher-complexity command
        var targetCommand = PromoteCommand(from.Command, to.Command);

        var fromPromoted = PromoteSegment(from, targetCommand);
        var toPromoted = PromoteSegment(to, targetCommand);

        return (
            new MorphSegment(targetCommand, fromPromoted, fromPromoted),
            new MorphSegment(targetCommand, toPromoted, toPromoted)
        );
    }

    private static PathCommand PromoteCommand(PathCommand a, PathCommand b)
    {
        // Close stays Close; both must be Close to stay Close
        if (a == PathCommand.Close && b == PathCommand.Close)
        {
            return PathCommand.Close;
        }

        if (a == PathCommand.Close || b == PathCommand.Close)
        {
            // One is close, the other isn't — use LineTo as a degenerate
            return PathCommand.LineTo;
        }

        // MoveTo and LineTo both promote to CubicTo when combined with CubicTo
        if (a == PathCommand.CubicTo || b == PathCommand.CubicTo)
        {
            return PathCommand.CubicTo;
        }

        if (a == PathCommand.QuadTo || b == PathCommand.QuadTo)
        {
            return PathCommand.QuadTo;
        }

        if (a == PathCommand.LineTo || b == PathCommand.LineTo)
        {
            return PathCommand.LineTo;
        }

        return PathCommand.MoveTo;
    }

    private static PointF[] PromoteSegment(PathSegment seg, PathCommand target)
    {
        if (seg.Command == target)
        {
            return seg.Points.Length > 0 ? seg.Points : [];
        }

        return (seg.Command, target) switch
        {
            // MoveTo → LineTo: single point stays single point
            (PathCommand.MoveTo, PathCommand.LineTo) =>
                seg.Points.Length > 0 ? [seg.Points[0]] : [new PointF(0f, 0f)],

            // LineTo → CubicTo: add control points at 1/3 and 2/3 of the line
            (PathCommand.LineTo, PathCommand.CubicTo) =>
                PromoteLineToCubic(seg),

            // MoveTo → CubicTo
            (PathCommand.MoveTo, PathCommand.CubicTo) =>
                PromoteLineToCubic(seg),

            // QuadTo → CubicTo: exact conversion
            (PathCommand.QuadTo, PathCommand.CubicTo) =>
                PromoteQuadToCubic(seg),

            // LineTo → QuadTo: add control point at midpoint
            (PathCommand.LineTo, PathCommand.QuadTo) =>
                PromoteLineToQuad(seg),

            // MoveTo → QuadTo
            (PathCommand.MoveTo, PathCommand.QuadTo) =>
                PromoteLineToQuad(seg),

            // Close: no points
            (_, PathCommand.Close) => [],
            (PathCommand.Close, _) =>
                seg.Points.Length > 0 ? [seg.Points[0]] : [new PointF(0f, 0f)],

            _ => seg.Points,
        };
    }

    private static PointF[] PromoteLineToCubic(PathSegment seg)
    {
        // From and to point — if MoveTo, the "from" is 0,0 (or same point)
        PointF from = new(0f, 0f);
        PointF to = seg.Points.Length > 0 ? seg.Points[^1] : new PointF(0f, 0f);

        // Control points at 1/3 and 2/3 of the line
        var cp1 = new PointF(
            from.X + (to.X - from.X) / 3f,
            from.Y + (to.Y - from.Y) / 3f
        );
        var cp2 = new PointF(
            from.X + 2f * (to.X - from.X) / 3f,
            from.Y + 2f * (to.Y - from.Y) / 3f
        );

        return [cp1, cp2, to];
    }

    private static PointF[] PromoteLineToQuad(PathSegment seg)
    {
        PointF from = new(0f, 0f);
        PointF to = seg.Points.Length > 0 ? seg.Points[^1] : new PointF(0f, 0f);

        var cp = new PointF(
            (from.X + to.X) / 2f,
            (from.Y + to.Y) / 2f
        );

        return [cp, to];
    }

    private static PointF[] PromoteQuadToCubic(PathSegment seg)
    {
        if (seg.Points.Length < 2)
        {
            return [new PointF(0f, 0f), new PointF(0f, 0f), new PointF(0f, 0f)];
        }

        PointF from = new(0f, 0f);
        PointF qp = seg.Points[0]; // quad control point
        PointF to = seg.Points[1]; // end point

        // Exact quadratic-to-cubic conversion:
        // CP1 = from + 2/3 * (qp - from)
        // CP2 = to   + 2/3 * (qp - to)
        var cp1 = new PointF(
            from.X + 2f / 3f * (qp.X - from.X),
            from.Y + 2f / 3f * (qp.Y - from.Y)
        );
        var cp2 = new PointF(
            to.X + 2f / 3f * (qp.X - to.X),
            to.Y + 2f / 3f * (qp.Y - to.Y)
        );

        return [cp1, cp2, to];
    }
}

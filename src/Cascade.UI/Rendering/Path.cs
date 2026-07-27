namespace Cascade.UI;

/// <summary>
/// An immutable 2D path composed of line segments, curves, and arcs.
/// Created via <see cref="PathBuilder"/> or convenience factory methods.
/// Safe to cache and reuse across frames.
/// </summary>
public sealed class Path
{
    // Path command tags — must match the Etch path decoder (EtchBackend.CompilePath)
    internal const byte CmdMoveTo = 0;
    internal const byte CmdLineTo = 1;
    internal const byte CmdCubicTo = 2;
    internal const byte CmdQuadTo = 3;
    internal const byte CmdClose = 4;

    /// <summary>
    /// The raw command buffer. Each byte is a command tag (MoveTo=0, LineTo=1,
    /// CubicTo=2, QuadTo=3, Close=4).
    /// </summary>
    internal ReadOnlyMemory<byte> Commands { get; }

    /// <summary>
    /// The raw data buffer. Float values consumed per command:
    /// MoveTo=2, LineTo=2, CubicTo=6, QuadTo=4, Close=0.
    /// </summary>
    internal ReadOnlyMemory<float> Data { get; }

    internal Path(ReadOnlyMemory<byte> commands, ReadOnlyMemory<float> data)
    {
        Commands = commands;
        Data = data;
    }

    /// <summary>Creates a circle path approximated with four cubic Bézier curves.</summary>
    public static Path Circle(Point center, float radius)
    {
        // Kappa: the magic number for approximating a circle with cubics.
        // 4 * (sqrt(2) - 1) / 3 ≈ 0.5522847498
        const float kappa = 0.5522847498f;
        float k = radius * kappa;
        float cx = center.X;
        float cy = center.Y;

        var builder = new PathBuilder();
        builder.MoveTo(new Point(cx + radius, cy));
        builder.CubicTo(
            new Point(cx + radius, cy + k),
            new Point(cx + k, cy + radius),
            new Point(cx, cy + radius));
        builder.CubicTo(
            new Point(cx - k, cy + radius),
            new Point(cx - radius, cy + k),
            new Point(cx - radius, cy));
        builder.CubicTo(
            new Point(cx - radius, cy - k),
            new Point(cx - k, cy - radius),
            new Point(cx, cy - radius));
        builder.CubicTo(
            new Point(cx + k, cy - radius),
            new Point(cx + radius, cy - k),
            new Point(cx + radius, cy));
        builder.Close();
        return builder.Build();
    }

    /// <summary>Creates a rectangle path.</summary>
    public static Path Rect(Rect rect)
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Point(rect.X, rect.Y));
        builder.LineTo(new Point(rect.Right, rect.Y));
        builder.LineTo(new Point(rect.Right, rect.Bottom));
        builder.LineTo(new Point(rect.X, rect.Bottom));
        builder.Close();
        return builder.Build();
    }

    /// <summary>Creates a rounded rectangle path with uniform corner radius.</summary>
    public static Path RoundedRect(Rect rect, float radius)
    {
        return RoundedRect(rect, radius, radius, radius, radius);
    }

    /// <summary>Creates a rounded rectangle path with per-corner radii.</summary>
    public static Path RoundedRect(Rect rect, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        const float kappa = 0.5522847498f;
        float x = rect.X;
        float y = rect.Y;
        float r = rect.Right;
        float b = rect.Bottom;

        // Clamp radii so they don't exceed half the rect dimension
        float maxRadiusH = rect.Width / 2f;
        float maxRadiusV = rect.Height / 2f;
        float maxRadius = MathF.Min(maxRadiusH, maxRadiusV);
        topLeft = MathF.Min(topLeft, maxRadius);
        topRight = MathF.Min(topRight, maxRadius);
        bottomRight = MathF.Min(bottomRight, maxRadius);
        bottomLeft = MathF.Min(bottomLeft, maxRadius);

        var pb = new PathBuilder();

        // Start at top-left corner, after the rounded part
        pb.MoveTo(new Point(x + topLeft, y));

        // Top edge → top-right corner
        pb.LineTo(new Point(r - topRight, y));
        if (topRight > 0)
        {
            float k = topRight * kappa;
            pb.CubicTo(
                new Point(r - topRight + k, y),
                new Point(r, y + topRight - k),
                new Point(r, y + topRight));
        }

        // Right edge → bottom-right corner
        pb.LineTo(new Point(r, b - bottomRight));
        if (bottomRight > 0)
        {
            float k = bottomRight * kappa;
            pb.CubicTo(
                new Point(r, b - bottomRight + k),
                new Point(r - bottomRight + k, b),
                new Point(r - bottomRight, b));
        }

        // Bottom edge → bottom-left corner
        pb.LineTo(new Point(x + bottomLeft, b));
        if (bottomLeft > 0)
        {
            float k = bottomLeft * kappa;
            pb.CubicTo(
                new Point(x + bottomLeft - k, b),
                new Point(x, b - bottomLeft + k),
                new Point(x, b - bottomLeft));
        }

        // Left edge → top-left corner
        pb.LineTo(new Point(x, y + topLeft));
        if (topLeft > 0)
        {
            float k = topLeft * kappa;
            pb.CubicTo(
                new Point(x, y + topLeft - k),
                new Point(x + topLeft - k, y),
                new Point(x + topLeft, y));
        }

        pb.Close();
        return pb.Build();
    }

    /// <summary>Creates an arc path.</summary>
    public static Path Arc(Point center, float radius, Angle startAngle, Angle sweepAngle)
    {
        float sweepDeg = sweepAngle.InDegrees;
        if (MathF.Abs(sweepDeg) < 0.001f)
        {
            // Degenerate arc — return a single-point path
            float startRad = startAngle.InRadians;
            float sx = center.X + radius * MathF.Cos(startRad);
            float sy = center.Y + radius * MathF.Sin(startRad);
            var pb = new PathBuilder();
            pb.MoveTo(new Point(sx, sy));
            return pb.Build();
        }

        var builder = new PathBuilder();

        // Split the arc into segments of at most 90 degrees, each approximated
        // by a cubic Bézier. The formula for the control point distance is:
        // k = (4/3) * tan(angle/4)
        float remaining = sweepAngle.InRadians;
        float current = startAngle.InRadians;
        bool first = true;

        while (MathF.Abs(remaining) > 0.001f)
        {
            // Take at most 90 degrees per segment
            float maxSegment = MathF.PI / 2f;
            float segment = remaining > 0
                ? MathF.Min(remaining, maxSegment)
                : MathF.Max(remaining, -maxSegment);

            float halfSegment = segment / 2f;
            float kFactor = 4f / 3f * MathF.Tan(halfSegment / 2f);

            float cos0 = MathF.Cos(current);
            float sin0 = MathF.Sin(current);
            float cos1 = MathF.Cos(current + segment);
            float sin1 = MathF.Sin(current + segment);

            float x0 = center.X + radius * cos0;
            float y0 = center.Y + radius * sin0;
            float x1 = center.X + radius * cos1;
            float y1 = center.Y + radius * sin1;

            // Control points are tangent to the circle, scaled by kFactor
            float cp1x = x0 - kFactor * radius * sin0;
            float cp1y = y0 + kFactor * radius * cos0;
            float cp2x = x1 + kFactor * radius * sin1;
            float cp2y = y1 - kFactor * radius * cos1;

            if (first)
            {
                builder.MoveTo(new Point(x0, y0));
                first = false;
            }

            builder.CubicTo(
                new Point(cp1x, cp1y),
                new Point(cp2x, cp2y),
                new Point(x1, y1));

            current += segment;
            remaining -= segment;
        }

        return builder.Build();
    }

    /// <summary>Creates a star path.</summary>
    public static Path Star(Point center, float outerRadius, float innerRadius, int points = 5)
    {
        if (points < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Star must have at least 2 points.");
        }

        var builder = new PathBuilder();
        int totalVertices = points * 2;
        float angleStep = MathF.PI / points; // Half-step between outer and inner

        // Start from top (-π/2 so first point is at 12 o'clock)
        float startOffset = -MathF.PI / 2f;

        for (int i = 0; i < totalVertices; i++)
        {
            float angle = startOffset + i * angleStep;
            float r = (i % 2 == 0) ? outerRadius : innerRadius;
            float x = center.X + r * MathF.Cos(angle);
            float y = center.Y + r * MathF.Sin(angle);

            if (i == 0)
            {
                builder.MoveTo(new Point(x, y));
            }
            else
            {
                builder.LineTo(new Point(x, y));
            }
        }

        builder.Close();
        return builder.Build();
    }

    /// <summary>Creates a regular polygon path.</summary>
    public static Path RegularPolygon(Point center, float radius, int sides = 6)
    {
        if (sides < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(sides), "Polygon must have at least 3 sides.");
        }

        var builder = new PathBuilder();
        float angleStep = 2f * MathF.PI / sides;

        // Start from top (-π/2 so first vertex is at 12 o'clock)
        float startOffset = -MathF.PI / 2f;

        for (int i = 0; i < sides; i++)
        {
            float angle = startOffset + i * angleStep;
            float x = center.X + radius * MathF.Cos(angle);
            float y = center.Y + radius * MathF.Sin(angle);

            if (i == 0)
            {
                builder.MoveTo(new Point(x, y));
            }
            else
            {
                builder.LineTo(new Point(x, y));
            }
        }

        builder.Close();
        return builder.Build();
    }
}

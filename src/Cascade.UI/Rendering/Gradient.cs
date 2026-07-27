namespace Cascade.UI;

/// <summary>
/// A gradient definition for filling shapes. Supports linear, radial,
/// and sweep (conic) gradient types via static factory methods.
/// </summary>
public sealed class Gradient
{
    internal GradientKind GradientType { get; }
    internal IReadOnlyList<GradientStop> Stops { get; }
    internal Angle Angle { get; }
    internal Point From { get; }
    internal Point To { get; }
    internal Point Center { get; }
    internal float GradientRadius { get; }

    private Gradient(GradientKind type, IReadOnlyList<GradientStop> stops,
        Angle angle = default, Point from = default, Point to = default,
        Point center = default, float radius = 0f)
    {
        GradientType = type;
        Stops = stops;
        Angle = angle;
        From = from;
        To = to;
        Center = center;
        GradientRadius = radius;
    }

    /// <summary>
    /// Creates a linear gradient between two points.
    /// </summary>
    public static Gradient Linear(Point from, Point to, params GradientStop[] stops)
    {
        return new Gradient(GradientKind.Linear, stops, from: from, to: to);
    }

    /// <summary>
    /// Creates a linear gradient at the specified angle.
    /// </summary>
    public static Gradient Linear(Angle angle, params GradientStop[] stops)
    {
        return new Gradient(GradientKind.Linear, stops, angle: angle);
    }

    /// <summary>
    /// Creates a radial gradient from a center point outward.
    /// </summary>
    public static Gradient Radial(Point center, float radius, params GradientStop[] stops)
    {
        return new Gradient(GradientKind.Radial, stops, center: center, radius: radius);
    }

    /// <summary>
    /// Creates a sweep (conic) gradient around a center point.
    /// </summary>
    public static Gradient Sweep(Point center, Angle startAngle, params GradientStop[] stops)
    {
        return new Gradient(GradientKind.Sweep, stops, angle: startAngle, center: center);
    }
}

/// <summary>
/// The type of gradient.
/// </summary>
internal enum GradientKind
{
    Linear,
    Radial,
    Sweep,
}

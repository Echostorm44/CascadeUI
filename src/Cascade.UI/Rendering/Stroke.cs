namespace Cascade.UI;

/// <summary>
/// Describes how a path outline is rendered: color, width, cap style,
/// join style, and optional dash pattern.
/// </summary>
/// <remarks>
/// <see cref="Stroke"/> is a small, immutable value type so that construction
/// in paint hot paths (one or more per node per frame) does not allocate on
/// the managed heap. Pass a <see cref="Nullable{Stroke}"/> (i.e.
/// <c>Stroke?</c>) when stroking is optional.
/// </remarks>
public readonly struct Stroke
{
    /// <summary>
    /// Creates a stroke with the specified color and width, with default
    /// <see cref="StrokeCap.Butt"/> and <see cref="StrokeJoin.Miter"/>.
    /// </summary>
    public Stroke(ColorValue color, float width)
    {
        Color = color;
        Width = width;
        Cap = StrokeCap.Butt;
        Join = StrokeJoin.Miter;
        Dash = null;
    }

    /// <summary>
    /// Creates a stroke with full control over cap and join styles and an
    /// optional dash pattern.
    /// </summary>
    public Stroke(ColorValue color, float width, StrokeCap cap, StrokeJoin join, DashPattern? dash = null)
    {
        Color = color;
        Width = width;
        Cap = cap;
        Join = join;
        Dash = dash;
    }

    /// <summary>The stroke color.</summary>
    public ColorValue Color { get; }

    /// <summary>The stroke width in logical pixels.</summary>
    public float Width { get; }

    /// <summary>How open path ends are rendered.</summary>
    public StrokeCap Cap { get; }

    /// <summary>How path segment joins are rendered.</summary>
    public StrokeJoin Join { get; }

    /// <summary>Optional dash pattern. Null for solid strokes.</summary>
    public DashPattern? Dash { get; }
}

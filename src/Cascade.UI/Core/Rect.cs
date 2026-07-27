namespace Cascade.UI;

/// <summary>
/// An immutable axis-aligned rectangle in logical pixel coordinates.
/// </summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>The left edge (same as X).</summary>
    public float Left => X;

    /// <summary>The top edge (same as Y).</summary>
    public float Top => Y;

    /// <summary>The right edge (X + Width).</summary>
    public float Right => X + Width;

    /// <summary>The bottom edge (Y + Height).</summary>
    public float Bottom => Y + Height;

    /// <summary>The center point of the rectangle.</summary>
    public Point Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>The size of the rectangle (Width, Height).</summary>
    public Size Size => new(Width, Height);

    /// <summary>
    /// Returns true if the given point is inside this rectangle (inclusive of edges).
    /// </summary>
    public bool Contains(Point point)
    {
        return point.X >= X && point.X <= Right
            && point.Y >= Y && point.Y <= Bottom;
    }

    /// <summary>
    /// Returns true if this rectangle overlaps with <paramref name="other"/>.
    /// </summary>
    public bool Intersects(Rect other)
    {
        return X < other.Right && Right > other.X
            && Y < other.Bottom && Bottom > other.Y;
    }

    /// <summary>
    /// Returns the intersection of this rectangle with <paramref name="other"/>,
    /// or a zero-size rect if they do not overlap.
    /// </summary>
    public Rect Intersect(Rect other)
    {
        float left = Math.Max(X, other.X);
        float top = Math.Max(Y, other.Y);
        float right = Math.Min(Right, other.Right);
        float bottom = Math.Min(Bottom, other.Bottom);

        if (right <= left || bottom <= top)
        {
            return default;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Returns the smallest rectangle that contains both this and <paramref name="other"/>.
    /// </summary>
    public Rect Union(Rect other)
    {
        float left = Math.Min(X, other.X);
        float top = Math.Min(Y, other.Y);
        float right = Math.Max(Right, other.Right);
        float bottom = Math.Max(Bottom, other.Bottom);

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Returns a rectangle expanded by <paramref name="amount"/> on all sides.
    /// </summary>
    public Rect Inflate(float amount)
    {
        return new Rect(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);
    }

    /// <summary>
    /// Returns a rectangle expanded by the given <paramref name="insets"/> on each side.
    /// </summary>
    public Rect Inflate(EdgeInsets insets)
    {
        return new Rect(
            X - insets.Left,
            Y - insets.Top,
            Width + insets.Horizontal,
            Height + insets.Vertical);
    }
}

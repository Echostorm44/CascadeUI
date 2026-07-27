namespace Cascade.UI;

/// <summary>
/// An immutable 2D size in logical pixels.
/// </summary>
public readonly record struct Size(float Width, float Height)
{
    /// <summary>
    /// A zero-size value (0, 0).
    /// </summary>
    public static readonly Size Zero = new(0, 0);

    /// <summary>
    /// A sentinel size that fills the parent's available space in both axes.
    /// The layout engine resolves this to the actual parent-allocated size.
    /// </summary>
    public static readonly Size Fill = new(float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>
    /// Creates a size with equal width and height.
    /// </summary>
    public static Size Square(float side)
    {
        return new Size(side, side);
    }

    /// <summary>
    /// The total area (Width × Height).
    /// </summary>
    public float Area => Width * Height;

    /// <summary>
    /// True if either dimension is zero or negative.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

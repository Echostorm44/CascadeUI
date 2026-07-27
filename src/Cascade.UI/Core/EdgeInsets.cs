namespace Cascade.UI;

/// <summary>
/// Immutable insets (padding, margin) for each side of a rectangle.
/// </summary>
public readonly record struct EdgeInsets(float Top, float Right, float Bottom, float Left)
{
    /// <summary>
    /// Zero insets on all sides.
    /// </summary>
    public static readonly EdgeInsets Zero = new(0, 0, 0, 0);

    /// <summary>
    /// Creates insets with the same value on all four sides.
    /// </summary>
    public static EdgeInsets All(float value)
    {
        return new EdgeInsets(value, value, value, value);
    }

    /// <summary>
    /// Creates insets with symmetric horizontal and vertical values.
    /// </summary>
    public static EdgeInsets Symmetric(float horizontal = 0, float vertical = 0)
    {
        return new EdgeInsets(vertical, horizontal, vertical, horizontal);
    }

    /// <summary>
    /// Creates insets with individual values per side, defaulting to zero.
    /// </summary>
    public static EdgeInsets Only(float top = 0, float right = 0, float bottom = 0, float left = 0)
    {
        return new EdgeInsets(top, right, bottom, left);
    }

    /// <summary>
    /// The total horizontal insets (Left + Right).
    /// </summary>
    public float Horizontal => Left + Right;

    /// <summary>
    /// The total vertical insets (Top + Bottom).
    /// </summary>
    public float Vertical => Top + Bottom;
}

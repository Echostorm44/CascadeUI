namespace Cascade.UI;

/// <summary>
/// Constraints that flow from parent to child during layout. Defines the
/// minimum and maximum size a child is allowed to be on each axis.
/// </summary>
public readonly record struct LayoutConstraints(
    float MinWidth,
    float MaxWidth,
    float MinHeight,
    float MaxHeight)
{
    /// <summary>
    /// Creates tight constraints where min equals max on both axes.
    /// The child must be exactly this size.
    /// </summary>
    public static LayoutConstraints Tight(Size size)
    {
        return new LayoutConstraints(size.Width, size.Width, size.Height, size.Height);
    }

    /// <summary>
    /// Creates loose constraints with min = 0 and max = the given size.
    /// The child can be any size up to the maximum.
    /// </summary>
    public static LayoutConstraints Loose(Size maxSize)
    {
        return new LayoutConstraints(0, maxSize.Width, 0, maxSize.Height);
    }

    /// <summary>
    /// Creates unbounded constraints (min = 0, max = infinity).
    /// The child can be any size it wants.
    /// </summary>
    public static LayoutConstraints Unbounded()
    {
        return new LayoutConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity);
    }

    /// <summary>
    /// Creates constraints with a tight width and flexible height.
    /// </summary>
    public static LayoutConstraints TightWidth(float width, float minHeight, float maxHeight)
    {
        return new LayoutConstraints(width, width, minHeight, maxHeight);
    }

    /// <summary>
    /// Creates constraints with a tight height and flexible width.
    /// </summary>
    public static LayoutConstraints TightHeight(float height, float minWidth, float maxWidth)
    {
        return new LayoutConstraints(minWidth, maxWidth, height, height);
    }

    /// <summary>
    /// True if both axes are tightly constrained (min equals max).
    /// </summary>
    public bool IsTight => MinWidth == MaxWidth && MinHeight == MaxHeight;

    /// <summary>
    /// Clamps the desired size to satisfy these constraints on each axis.
    /// </summary>
    public Size Constrain(Size desired)
    {
        return new Size(
            ConstrainWidth(desired.Width),
            ConstrainHeight(desired.Height));
    }

    /// <summary>
    /// Clamps a width value to [MinWidth, MaxWidth].
    /// </summary>
    public float ConstrainWidth(float width)
    {
        return Math.Clamp(width, MinWidth, MaxWidth);
    }

    /// <summary>
    /// Clamps a height value to [MinHeight, MaxHeight].
    /// </summary>
    public float ConstrainHeight(float height)
    {
        return Math.Clamp(height, MinHeight, MaxHeight);
    }
}

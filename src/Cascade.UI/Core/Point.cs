namespace Cascade.UI;

/// <summary>
/// An immutable 2D point in logical pixel coordinates.
/// </summary>
public readonly record struct Point(float X, float Y)
{
    /// <summary>
    /// The origin point (0, 0).
    /// </summary>
    public static readonly Point Zero = new(0, 0);
}

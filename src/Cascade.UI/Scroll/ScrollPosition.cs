namespace Cascade.UI;

/// <summary>
/// An immutable 2D scroll offset in logical pixels. Reactive — reading in
/// <see cref="Component.Render"/> registers a lightweight scroll-position
/// binding that updates only the specific property that depends on it,
/// without re-rendering the entire component tree.
/// </summary>
public readonly record struct ScrollPosition(float X, float Y)
{
    /// <summary>
    /// Zero scroll offset (0, 0).
    /// </summary>
    public static readonly ScrollPosition Zero = new(0, 0);
}

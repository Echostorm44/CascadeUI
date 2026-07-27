namespace Cascade.UI;

/// <summary>
/// Provides purpose-built loading placeholder nodes that match the shape of
/// the content they replace. Skeleton nodes pulse with a subtle animation
/// to indicate loading (respects reduced-motion preferences).
/// </summary>
public static class Skeleton
{
    /// <summary>
    /// A rectangular skeleton placeholder.
    /// </summary>
    /// <param name="width">Width in logical pixels.</param>
    /// <param name="height">Height in logical pixels.</param>
    public static Node Rect(float width, float height)
    {
        return new SkeletonNode(SkeletonShape.Rectangle, width, height);
    }

    /// <summary>
    /// A circular skeleton placeholder, typically used for avatars.
    /// </summary>
    /// <param name="diameter">The diameter of the circle in logical pixels.</param>
    public static Node Circle(float diameter)
    {
        return new SkeletonNode(SkeletonShape.Circle, diameter, diameter);
    }

    /// <summary>
    /// Multiple lines of text skeleton placeholder.
    /// </summary>
    /// <param name="count">Number of skeleton lines.</param>
    /// <param name="lineHeight">Height of each line in logical pixels.</param>
    /// <param name="spacing">Spacing between lines in logical pixels.</param>
    public static Node Lines(int count, float lineHeight = 16, float spacing = 8)
    {
        var totalHeight = (count * lineHeight) + (Math.Max(0, count - 1) * spacing);
        return new SkeletonNode(SkeletonShape.Lines, 0, totalHeight, count, spacing);
    }

    /// <summary>
    /// A card-shaped skeleton placeholder.
    /// </summary>
    /// <param name="width">Width in logical pixels.</param>
    /// <param name="height">Height in logical pixels.</param>
    public static Node Card(float width, float height)
    {
        return new SkeletonNode(SkeletonShape.Card, width, height);
    }
}

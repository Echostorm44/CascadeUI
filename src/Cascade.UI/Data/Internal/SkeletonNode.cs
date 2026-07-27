namespace Cascade.UI;

/// <summary>
/// The shape of a skeleton placeholder.
/// </summary>
internal enum SkeletonShape
{
    Rectangle,
    Circle,
    Lines,
    Card
}

/// <summary>
/// A node representing an animated skeleton loading placeholder.
/// The rendering backend draws this as a pulsing shape that indicates
/// content is loading (respects reduced-motion preferences).
/// </summary>
internal sealed class SkeletonNode : Node
{
    internal SkeletonShape Shape { get; }
    internal float Width { get; }
    internal float Height { get; }
    internal int LineCount { get; }
    internal float LineSpacing { get; }

    internal SkeletonNode(SkeletonShape shape, float width, float height, int lineCount = 1, float lineSpacing = 0)
    {
        Shape = shape;
        Width = width;
        Height = height;
        LineCount = lineCount;
        LineSpacing = lineSpacing;
    }
}

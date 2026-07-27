namespace Cascade.UI;

/// <summary>
/// A layout node that arranges its children horizontally (left to right).
/// Supports spacing, main/cross axis alignment, wrapping, and flex grow.
/// </summary>
public sealed class Row : Node
{
    /// <summary>
    /// Creates a horizontal layout container.
    /// </summary>
    /// <param name="spacing">Space between children in logical pixels.</param>
    /// <param name="mainAxisAlignment">How children are distributed along the horizontal axis.</param>
    /// <param name="crossAxisAlignment">How children are aligned along the vertical axis.</param>
    /// <param name="children">Child nodes to lay out horizontally.</param>
    public Row(
        float spacing = 0,
        MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start,
        CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Start,
        params Node[] children)
    {
        Spacing = spacing;
        MainAxisAlignment = mainAxisAlignment;
        CrossAxisAlignment = crossAxisAlignment;
        Children = children;
    }

    /// <summary>Space between children in logical pixels.</summary>
    public float Spacing { get; }

    /// <summary>How children are distributed along the main (horizontal) axis.</summary>
    public MainAxisAlignment MainAxisAlignment { get; }

    /// <summary>How children are aligned along the cross (vertical) axis.</summary>
    public CrossAxisAlignment CrossAxisAlignment { get; }

    /// <summary>The child nodes.</summary>
    public IReadOnlyList<Node> Children { get; }
}

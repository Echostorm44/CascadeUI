namespace Cascade.UI;

/// <summary>
/// A layout node that arranges its children vertically (top to bottom).
/// Supports spacing, main/cross axis alignment, wrapping, and flex grow.
/// </summary>
public sealed class Column : Node
{
    /// <summary>
    /// Creates a vertical layout container.
    /// </summary>
    /// <param name="spacing">Space between children in logical pixels.</param>
    /// <param name="mainAxisAlignment">How children are distributed along the vertical axis.</param>
    /// <param name="crossAxisAlignment">
    /// How children are aligned along the horizontal (cross) axis. Defaults to
    /// <see cref="CrossAxisAlignment.Stretch"/>: children fill the column's width,
    /// the natural expectation for vertical stacks (form fields, cards, CTAs).
    /// Use <see cref="CrossAxisAlignment.Center"/>/<see cref="CrossAxisAlignment.Start"/>/
    /// <see cref="CrossAxisAlignment.End"/> to let children hug their content and be
    /// positioned instead.
    /// </param>
    /// <param name="children">Child nodes to lay out vertically.</param>
    public Column(
        float spacing = 0,
        MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start,
        CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Stretch,
        params Node[] children)
    {
        Spacing = spacing;
        MainAxisAlignment = mainAxisAlignment;
        CrossAxisAlignment = crossAxisAlignment;
        Children = children;
    }

    /// <summary>Space between children in logical pixels.</summary>
    public float Spacing { get; }

    /// <summary>How children are distributed along the main (vertical) axis.</summary>
    public MainAxisAlignment MainAxisAlignment { get; }

    /// <summary>How children are aligned along the cross (horizontal) axis.</summary>
    public CrossAxisAlignment CrossAxisAlignment { get; }

    /// <summary>The child nodes.</summary>
    public IReadOnlyList<Node> Children { get; }
}

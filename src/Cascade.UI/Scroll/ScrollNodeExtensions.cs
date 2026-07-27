namespace Cascade.UI;

/// <summary>
/// Extension methods for attaching scroll-related behaviors (sticky pinning,
/// snap alignment) to any <see cref="Node"/> used inside a <see cref="ScrollView"/>.
/// </summary>
public static class ScrollNodeExtensions
{
    /// <summary>
    /// Makes this node sticky — it pins to the specified edge of the viewport
    /// when scrolled past. When the next sticky element approaches, it pushes
    /// this one and takes its place.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="node">The node to make sticky.</param>
    /// <param name="edge">The viewport edge to pin to. Default: <see cref="StickyEdge.Top"/>.</param>
    /// <returns>The node, for fluent chaining.</returns>
    public static T Sticky<T>(this T node, StickyEdge edge = StickyEdge.Top) where T : Node
    {
        var data = EnsureScrollData(node);
        data.IsSticky = true;
        data.StickyEdge = edge;
        return node;
    }

    /// <summary>
    /// Declares a custom snap alignment for this child within a snapping
    /// <see cref="ScrollView"/>. Overrides the ScrollView's default
    /// <see cref="SnapAlignment"/>.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="node">The node to configure.</param>
    /// <param name="alignment">The snap alignment for this child.</param>
    /// <returns>The node, for fluent chaining.</returns>
    public static T SnapAlign<T>(this T node, SnapAlignment alignment) where T : Node
    {
        var data = EnsureScrollData(node);
        data.SnapAlignmentOverride = alignment;
        return node;
    }

    /// <summary>
    /// Excludes this child from snap point consideration within a snapping
    /// <see cref="ScrollView"/>. The scroll position will not snap to this node.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="node">The node to exclude from snapping.</param>
    /// <returns>The node, for fluent chaining.</returns>
    public static T SnapExclude<T>(this T node) where T : Node
    {
        var data = EnsureScrollData(node);
        data.IsSnapExcluded = true;
        return node;
    }

    private static ScrollNodeData EnsureScrollData(Node node)
    {
        node.LayoutData.ScrollData ??= new ScrollNodeData();
        return node.LayoutData.ScrollData;
    }
}

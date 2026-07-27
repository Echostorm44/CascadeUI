namespace Cascade.UI;

/// <summary>
/// Per-node storage for scroll-related modifiers: sticky pinning and snap alignment.
/// Attached to nodes via <see cref="ScrollNodeExtensions"/> and stored in
/// <see cref="LayoutNodeData"/>.
/// </summary>
internal sealed class ScrollNodeData
{
    /// <summary>Whether this node is sticky.</summary>
    internal bool IsSticky;

    /// <summary>The edge to pin this sticky node to.</summary>
    internal StickyEdge StickyEdge;

    /// <summary>Optional per-child snap alignment override.</summary>
    internal SnapAlignment? SnapAlignmentOverride;

    /// <summary>Whether this node is excluded from snap point consideration.</summary>
    internal bool IsSnapExcluded;
}

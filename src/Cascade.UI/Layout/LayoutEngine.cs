namespace Cascade.UI;

/// <summary>
/// The layout engine computes size and position for every node in the tree.
/// Single-pass, constraint-based: constraints flow down, sizes flow up.
/// </summary>
/// <remarks>
/// The layout engine visits each node exactly once in a depth-first traversal.
/// There is no second pass. The layout pass for a 1000-node tree must complete
/// in under 1ms.
/// </remarks>
public sealed class LayoutEngine
{
    private readonly DirtyTracker dirtyTracker = new();
    private readonly IntrinsicSizeCache intrinsicCache = new();

    /// <summary>
    /// The dirty tracker for incremental layout. Nodes marked dirty are
    /// re-measured; clean nodes retain cached results.
    /// </summary>
    internal DirtyTracker DirtyTracker => dirtyTracker;

    /// <summary>
    /// Cache for intrinsic size queries. Cleared each layout pass.
    /// </summary>
    internal IntrinsicSizeCache IntrinsicCache => intrinsicCache;

    /// <summary>
    /// Performs a full layout pass on the given node tree, starting from the
    /// root with the given constraints.
    /// </summary>
    /// <param name="root">The root node of the tree to lay out.</param>
    /// <param name="constraints">The top-level constraints (typically the window size).</param>
    public void Layout(Node root, LayoutConstraints constraints)
    {
        intrinsicCache.Clear();
        dirtyTracker.BeginLayoutPass();
        LayoutSolver.PerformLayout(root, constraints);
    }
}

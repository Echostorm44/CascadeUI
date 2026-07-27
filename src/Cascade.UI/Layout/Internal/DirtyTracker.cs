namespace Cascade.UI;

/// <summary>
/// Tracks which nodes need re-layout. Unchanged nodes retain cached layout
/// results. Uses a version counter and constraint hash comparison.
/// </summary>
internal sealed class DirtyTracker
{
    private int currentVersion;

    /// <summary>
    /// Increments the global layout version. Called at the start of each
    /// layout pass. Nodes whose version matches the current version are clean.
    /// </summary>
    internal int BeginLayoutPass()
    {
        return ++currentVersion;
    }

    /// <summary>
    /// Returns true if the node needs re-layout. A node is dirty if its
    /// layout version doesn't match the current pass or its constraints
    /// have changed.
    /// </summary>
    internal bool IsDirty(Node node, LayoutConstraints constraints)
    {
        var data = node.LayoutData;
        int hash = ComputeConstraintHash(constraints);

        if (data.LayoutVersion == currentVersion && data.LastConstraintHash == hash)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Marks a node as clean for the current layout pass with the given
    /// constraint hash.
    /// </summary>
    internal void MarkClean(Node node, LayoutConstraints constraints)
    {
        var data = node.LayoutData;
        data.LayoutVersion = currentVersion;
        data.LastConstraintHash = ComputeConstraintHash(constraints);
    }

    /// <summary>
    /// Forces a node to be dirty on the next layout pass by resetting
    /// its version.
    /// </summary>
    internal static void MarkDirty(Node node)
    {
        node.LayoutData.LayoutVersion = 0;
    }

    /// <summary>
    /// Computes a hash code from the constraint values for quick comparison.
    /// </summary>
    private static int ComputeConstraintHash(LayoutConstraints constraints)
    {
        return HashCode.Combine(
            constraints.MinWidth,
            constraints.MaxWidth,
            constraints.MinHeight,
            constraints.MaxHeight);
    }
}

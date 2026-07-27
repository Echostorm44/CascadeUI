namespace Cascade.UI;

/// <summary>
/// A responsive 2D grid layout that arranges children in rows and columns.
/// Supports adaptive column counts, fixed grids, and explicit column definitions.
/// </summary>
public sealed class Grid : Node
{
    /// <summary>
    /// Creates a grid layout container.
    /// </summary>
    /// <param name="columns">Column definition — adaptive, fixed count, or explicit.</param>
    /// <param name="spacing">Space between items in logical pixels (both axes).</param>
    /// <param name="children">Child nodes to arrange in the grid.</param>
    public Grid(
        GridColumnDef columns,
        float spacing = 0,
        params Node[] children)
    {
        Columns = columns;
        Spacing = spacing;
        Children = children;
    }

    /// <summary>The column definition for this grid.</summary>
    public GridColumnDef Columns { get; }

    /// <summary>Space between items in logical pixels.</summary>
    public float Spacing { get; }

    /// <summary>The child nodes.</summary>
    public IReadOnlyList<Node> Children { get; }
}

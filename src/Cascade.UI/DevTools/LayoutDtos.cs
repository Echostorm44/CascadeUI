using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Constraint flow data showing what constraints a node received
/// and what size it returned.
/// </summary>
/// <remarks>
/// Extracted from <see cref="LayoutPanel"/> so it remains visible to
/// <see cref="NodeTreeWalker"/> and the MCP tool surface in Release builds
/// compiled with <c>CASCADE_DEVTOOLS</c> (where the DEBUG-gated panel class
/// itself is not compiled).
/// </remarks>
public sealed class ConstraintFlow
{
    /// <summary>Node identifier.</summary>
    public required string NodeId { get; init; }

    /// <summary>Minimum width constraint received.</summary>
    public float MinWidth { get; init; }

    /// <summary>Maximum width constraint received.</summary>
    public float MaxWidth { get; init; }

    /// <summary>Minimum height constraint received.</summary>
    public float MinHeight { get; init; }

    /// <summary>Maximum height constraint received.</summary>
    public float MaxHeight { get; init; }

    /// <summary>Width the node returned after measurement.</summary>
    public float ReturnedWidth { get; init; }

    /// <summary>Height the node returned after measurement.</summary>
    public float ReturnedHeight { get; init; }

    /// <summary>Parent node type name.</summary>
    public string? ParentTypeName { get; init; }

    /// <summary>Parent source location.</summary>
    public string? ParentSourceLocation { get; init; }
}

/// <summary>
/// Flex distribution info for children in a Row or Column.
/// </summary>
public sealed class FlexDistribution
{
    /// <summary>Container node ID (the Row or Column).</summary>
    public required string ContainerId { get; init; }

    /// <summary>Total available space in the flex direction.</summary>
    public float TotalSpace { get; init; }

    /// <summary>Space consumed by non-flex children.</summary>
    public float FixedSpace { get; init; }

    /// <summary>Remaining space distributed among flex children.</summary>
    public float FlexSpace { get; init; }

    /// <summary>Per-child flex info.</summary>
    public IReadOnlyList<FlexChildInfo> Children { get; init; } = [];
}

/// <summary>
/// Info about one child in a flex container.
/// </summary>
public sealed class FlexChildInfo
{
    /// <summary>Child node ID.</summary>
    public required string NodeId { get; init; }

    /// <summary>Child type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Whether this child has Grow applied.</summary>
    public bool IsFlex { get; init; }

    /// <summary>Grow factor (0 if not flex).</summary>
    public float GrowFactor { get; init; }

    /// <summary>Space received in the flex direction.</summary>
    public float AllocatedSpace { get; init; }
}

/// <summary>
/// A node that overflows its parent bounds.
/// </summary>
public sealed class OverflowInfo
{
    /// <summary>Child node that overflows.</summary>
    public required string NodeId { get; init; }

    /// <summary>Parent node being overflowed.</summary>
    public required string ParentId { get; init; }

    /// <summary>Overflow amount on each edge (positive = overflowing).</summary>
    public float OverflowLeft { get; init; }
    public float OverflowRight { get; init; }
    public float OverflowTop { get; init; }
    public float OverflowBottom { get; init; }
}

/// <summary>
/// Grid track info for a Grid layout node.
/// </summary>
public sealed class GridInfo
{
    /// <summary>Grid node ID.</summary>
    public required string NodeId { get; init; }

    /// <summary>Column track positions and widths.</summary>
    public IReadOnlyList<GridTrack> Columns { get; init; } = [];

    /// <summary>Row track positions and heights.</summary>
    public IReadOnlyList<GridTrack> Rows { get; init; } = [];
}

/// <summary>A single track in a grid layout.</summary>
public sealed class GridTrack
{
    /// <summary>Track index.</summary>
    public int Index { get; init; }

    /// <summary>Start position in logical pixels.</summary>
    public float Start { get; init; }

    /// <summary>Track size in logical pixels.</summary>
    public float Size { get; init; }
}

#endif

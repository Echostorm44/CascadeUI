using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Layout debugging panel. Shows box model overlays, constraint flow,
/// flex distribution, overflow indicators, and grid lines for the
/// currently selected node.
/// </summary>
internal static class LayoutPanel
{
    // BoxModel, ConstraintFlow, FlexDistribution, FlexChildInfo, OverflowInfo,
    // GridInfo, and GridTrack are declared in standalone files under
    // #if CASCADE_DEVTOOLS so agents inspecting a Release + CascadeDevTools
    // build can consume them via NodeTreeWalker / MCP tools.
    // See: BoxModel.cs, LayoutDtos.cs

    /// <summary>Gets the box model for a node.</summary>
    public static BoxModel? GetBoxModel(string nodeId)
    {
        var node = NodeTreeWalker.FindNode(nodeId);
        if (node is null)
        {
            return null;
        }

        return NodeTreeWalker.GetBoxModel(node);
    }

    /// <summary>Gets the constraint flow for a node.</summary>
    public static ConstraintFlow? GetConstraintFlow(string nodeId)
    {
        var node = NodeTreeWalker.FindNode(nodeId);
        if (node is null)
        {
            return null;
        }

        return NodeTreeWalker.GetConstraintFlow(node);
    }

    /// <summary>
    /// Gets flex distribution for a Row or Column container.
    /// Returns null if the node is not a flex container.
    /// </summary>
    public static FlexDistribution? GetFlexDistribution(string containerId)
    {
        var node = NodeTreeWalker.FindNode(containerId);
        if (node is null)
        {
            return null;
        }

        return NodeTreeWalker.GetFlexDistribution(node);
    }

    /// <summary>
    /// Finds all nodes that overflow their parent bounds.
    /// </summary>
    public static IReadOnlyList<OverflowInfo> FindOverflows()
    {
        return NodeTreeWalker.FindOverflows();
    }

    /// <summary>
    /// Gets grid track info for a Grid node.
    /// Returns null if the node is not a Grid.
    /// </summary>
    public static GridInfo? GetGridInfo(string nodeId)
    {
        var node = NodeTreeWalker.FindNode(nodeId);
        if (node is null)
        {
            return null;
        }

        return NodeTreeWalker.GetGridInfo(node);
    }
}

#endif

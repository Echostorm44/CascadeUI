namespace Cascade.UI;

/// <summary>
/// A node in a tree map hierarchy. Each node has a label and a numeric value
/// that determines its area. Nodes may contain children for nested rectangles.
/// </summary>
public sealed class TreeMapNode
{
    /// <summary>Creates a tree map node with a label, value, and optional children.</summary>
    /// <param name="label">The display label for this rectangle.</param>
    /// <param name="value">The numeric value that determines the area of this rectangle.</param>
    /// <param name="children">Optional child nodes displayed as nested rectangles.</param>
    public TreeMapNode(string label, double value, IReadOnlyList<TreeMapNode>? children = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        Label = label;
        Value = value;
        Children = children ?? Array.Empty<TreeMapNode>();
    }

    /// <summary>Display label for this rectangle.</summary>
    public string Label { get; }

    /// <summary>Numeric value that determines the area of this rectangle.</summary>
    public double Value { get; }

    /// <summary>Child nodes displayed as nested rectangles.</summary>
    public IReadOnlyList<TreeMapNode> Children { get; }

    /// <summary>Optional color override for this node.</summary>
    internal ColorValue? ColorOverride { get; set; }

    /// <summary>Sets a custom color for this node.</summary>
    /// <param name="color">The color to use for this node's rectangle.</param>
    public TreeMapNode Color(ColorValue color)
    {
        ColorOverride = color;
        return this;
    }
}

/// <summary>
/// A tree map chart that renders hierarchical data as nested, area-proportional rectangles.
/// The area of each rectangle is proportional to its value, with children displayed
/// as nested subdivisions within their parent.
/// </summary>
public sealed class TreeMapChart : ChartBase<TreeMapChart>
{
    internal IReadOnlyList<TreeMapNode> nodesList;
    internal bool showLabels = true;
    internal float labelMinArea = 0.02f;
    internal float cellGap = 2f;
    internal float cellRadius = 2f;
    internal bool drillDownEnabled;
    internal Action<TreeMapNode>? onDrillDown;
    internal bool colorByDepth = true;

    /// <summary>Creates a tree map chart from a list of root nodes.</summary>
    /// <param name="data">The root-level nodes of the tree map hierarchy.</param>
    public TreeMapChart(IReadOnlyList<TreeMapNode> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        nodesList = data;
    }

    /// <summary>The root nodes in this chart.</summary>
    internal IReadOnlyList<TreeMapNode> Nodes => nodesList;

    /// <summary>Configures label visibility and the minimum area fraction required to show a label.</summary>
    /// <param name="show">True to show labels on rectangles.</param>
    /// <param name="minArea">Minimum fraction of total area (0–1) for a rectangle to receive a label.</param>
    public TreeMapChart Labels(bool show, float minArea = 0.02f)
    {
        showLabels = show;
        labelMinArea = minArea;
        return this;
    }

    /// <summary>Sets the gap in logical pixels between rectangles.</summary>
    /// <param name="gap">The gap size between rectangles.</param>
    public TreeMapChart CellGap(float gap)
    {
        cellGap = gap;
        return this;
    }

    /// <summary>Sets the corner radius of each rectangle.</summary>
    /// <param name="radius">The corner radius in logical pixels.</param>
    public TreeMapChart CellRadius(float radius)
    {
        cellRadius = radius;
        return this;
    }

    /// <summary>Enables or disables drill-down interaction for exploring child nodes.</summary>
    /// <param name="enabled">True to enable drill-down on click.</param>
    /// <param name="onDrillDown">Optional callback invoked when a node is drilled into.</param>
    public TreeMapChart DrillDown(bool enabled, Action<TreeMapNode>? onDrillDown = null)
    {
        drillDownEnabled = enabled;
        this.onDrillDown = onDrillDown;
        return this;
    }

    /// <summary>Controls whether rectangle colors vary by depth level in the hierarchy.</summary>
    /// <param name="enabled">True to cycle palette colors by depth.</param>
    public TreeMapChart ColorByDepth(bool enabled)
    {
        colorByDepth = enabled;
        return this;
    }
}

namespace Cascade.UI;

/// <summary>Controls how chart tooltips are displayed.</summary>
public enum TooltipMode
{
    /// <summary>Shows value for the single nearest data point.</summary>
    Single,

    /// <summary>Shows all series values at the cursor X position.</summary>
    All,

    /// <summary>Shows all series values with crosshair lines across the chart.</summary>
    Crosshair,

    /// <summary>Tooltips are disabled.</summary>
    None
}

/// <summary>Position of the chart legend relative to the chart area.</summary>
public enum LegendPosition
{
    Top,
    Bottom,
    Left,
    Right,
    None
}

/// <summary>Zoom interaction mode for charts.</summary>
public enum ZoomMode
{
    /// <summary>Zoom only on the X axis.</summary>
    X,

    /// <summary>Zoom only on the Y axis.</summary>
    Y,

    /// <summary>Zoom on both axes simultaneously.</summary>
    XY
}

/// <summary>Orientation of a bar chart.</summary>
public enum ChartOrientation
{
    /// <summary>Vertical bars (columns).</summary>
    Vertical,

    /// <summary>Horizontal bars.</summary>
    Horizontal
}

/// <summary>How multiple series bars are arranged in a bar chart.</summary>
public enum BarGroupMode
{
    /// <summary>Bars side by side for each category.</summary>
    Grouped,

    /// <summary>Bars stacked on top of each other.</summary>
    Stacked,

    /// <summary>Bars stacked with the Y axis normalized to 0–100%.</summary>
    StackedPercent
}

/// <summary>When to show data point markers on line and area charts.</summary>
public enum PointDisplay
{
    /// <summary>Always show data point markers.</summary>
    Always,

    /// <summary>Never show data point markers.</summary>
    Never,

    /// <summary>Show markers at low density, hide at high density.</summary>
    Auto
}

/// <summary>How to handle missing (null) values in line and area charts.</summary>
public enum MissingValueMode
{
    /// <summary>Break the line at null values.</summary>
    Gap,

    /// <summary>Skip over null values, connecting neighboring points.</summary>
    Connect,

    /// <summary>Treat null values as zero.</summary>
    Zero
}

/// <summary>Visual type for sparkline mini charts.</summary>
public enum SparklineType
{
    /// <summary>Continuous line connecting data points.</summary>
    Line,

    /// <summary>Vertical bars for each data point.</summary>
    Bar,

    /// <summary>Equal-height bars above (positive) or below (negative) the baseline.</summary>
    WinLoss
}

/// <summary>When chart animations are triggered.</summary>
public enum AnimateTrigger
{
    /// <summary>Animate on initial render only.</summary>
    Load,

    /// <summary>Animate only when data changes.</summary>
    DataChange,

    /// <summary>Animate on both initial load and data changes.</summary>
    Both,

    /// <summary>No animation.</summary>
    None
}

/// <summary>Line stroke style for chart series and reference lines.</summary>
public enum LineStyle
{
    Solid,
    Dashed,
    Dotted
}

/// <summary>Shape of data point markers on line, area, and scatter charts.</summary>
public enum PointShape
{
    Circle,
    Square,
    Diamond,
    Triangle,
    Cross
}

/// <summary>Type of statistical trendline overlay.</summary>
public enum TrendlineType
{
    Linear,
    Exponential,
    MovingAverage,
    Polynomial
}

/// <summary>Position of data labels on chart elements.</summary>
public enum DataLabelPosition
{
    /// <summary>Framework chooses the best position to avoid overlap.</summary>
    Auto,
    Inside,
    Outside,
    Center,
    Top,
    Bottom
}

/// <summary>How overlapping data labels are handled.</summary>
public enum DataLabelOverlap
{
    /// <summary>Hide lower-priority labels that would overlap.</summary>
    Hide,

    /// <summary>Show all labels regardless of overlap.</summary>
    Show,

    /// <summary>Stagger label positions to reduce overlap.</summary>
    Stagger
}

/// <summary>Anchor position for chart annotations relative to their data point.</summary>
public enum AnnotationAnchor
{
    Above,
    Below,
    Left,
    Right
}

/// <summary>Target axis for reference lines, reference bands, and series binding.</summary>
public enum ChartAxis
{
    X,
    Y,
    Y2
}

/// <summary>Text rendering mode for SVG chart exports.</summary>
public enum SvgTextMode
{
    /// <summary>Render text as selectable, searchable text elements.</summary>
    Text,

    /// <summary>Convert text to paths, preserving appearance without requiring fonts.</summary>
    Path
}

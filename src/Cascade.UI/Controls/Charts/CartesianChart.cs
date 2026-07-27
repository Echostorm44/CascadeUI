namespace Cascade.UI;

/// <summary>
/// Abstract base class for charts with Cartesian (X/Y) axes.
/// Extends <see cref="ChartBase{TSelf}"/> with axis configuration, zoom/pan,
/// data labels, reference lines/bands, annotations, and trendlines.
/// </summary>
/// <typeparam name="TSelf">The concrete chart type, enabling fluent method chaining.</typeparam>
public abstract class CartesianChart<TSelf> : ChartBase<TSelf> where TSelf : CartesianChart<TSelf>
{
    internal string? xAxisLabel;
    internal AxisFormat? xAxisFormat;
    internal double? xAxisMin;
    internal double? xAxisMax;
    internal bool xAxisGridLines;
    internal AxisTicks? xAxisTicks;
    internal string? yAxisLabel;
    internal AxisFormat? yAxisFormat;
    internal double? yAxisMin;
    internal double? yAxisMax;
    internal string? y2AxisLabel;
    internal AxisFormat? y2AxisFormat;
    internal bool zoomEnabled;
    internal ZoomMode zoomMode = ZoomMode.XY;
    internal bool zoomResetOnDoubleClick = true;
    internal bool dataLabelsEnabled;
    internal DataLabelPosition dataLabelPosition = DataLabelPosition.Auto;
    internal AxisFormat? dataLabelFormat;
    internal DataLabelOverlap dataLabelOverlap = DataLabelOverlap.Hide;
    internal List<ReferenceLineConfig>? referenceLines;
    internal List<ReferenceBandConfig>? referenceBands;
    internal List<AnnotationConfig>? annotations;
    internal TrendlineType? trendlineType;
    internal int? trendlinePeriod;
    internal int? trendlineDegree;
    internal ColorValue? trendlineColor;
    internal object? zoomStartX;
    internal object? zoomEndX;

    /// <summary>Configures the X axis label, format, range, grid lines, and tick placement.</summary>
    public TSelf XAxis(
        string? label = null,
        AxisFormat? format = null,
        double? min = null,
        double? max = null,
        bool gridLines = false,
        AxisTicks? ticks = null)
    {
        xAxisLabel = label;
        xAxisFormat = format;
        xAxisMin = min;
        xAxisMax = max;
        xAxisGridLines = gridLines;
        xAxisTicks = ticks;
        return (TSelf)this;
    }

    /// <summary>Configures the primary Y axis label, format, and range.</summary>
    public TSelf YAxis(
        string? label = null,
        AxisFormat? format = null,
        double? min = null,
        double? max = null)
    {
        yAxisLabel = label;
        yAxisFormat = format;
        yAxisMin = min;
        yAxisMax = max;
        return (TSelf)this;
    }

    /// <summary>Configures a secondary Y axis on the right side of the chart.</summary>
    public TSelf Y2Axis(
        string? label = null,
        AxisFormat? format = null)
    {
        y2AxisLabel = label;
        y2AxisFormat = format;
        return (TSelf)this;
    }

    /// <summary>Enables zoom and pan interaction on the chart.</summary>
    public TSelf ZoomPan(
        bool enabled = true,
        ZoomMode mode = ZoomMode.XY,
        bool resetOnDoubleClick = true)
    {
        zoomEnabled = enabled;
        zoomMode = mode;
        zoomResetOnDoubleClick = resetOnDoubleClick;
        return (TSelf)this;
    }

    /// <summary>Configures data labels displayed directly on chart elements.</summary>
    public TSelf DataLabels(
        bool enabled = true,
        DataLabelPosition position = DataLabelPosition.Auto,
        AxisFormat? format = null,
        DataLabelOverlap overlap = DataLabelOverlap.Hide)
    {
        dataLabelsEnabled = enabled;
        dataLabelPosition = position;
        dataLabelFormat = format;
        dataLabelOverlap = overlap;
        return (TSelf)this;
    }

    /// <summary>Adds a horizontal or vertical reference line at the specified value.</summary>
    public TSelf ReferenceLine(
        double value,
        string? label = null,
        ChartAxis axis = ChartAxis.Y,
        ColorValue? color = null,
        LineStyle style = LineStyle.Dashed)
    {
        referenceLines ??= [];
        referenceLines.Add(new ReferenceLineConfig(value, label, axis, color, style));
        return (TSelf)this;
    }

    /// <summary>Adds a shaded reference band between two axis values.</summary>
    public TSelf ReferenceBand(
        double from,
        double to,
        string? label = null,
        ChartAxis axis = ChartAxis.Y,
        ColorValue? color = null)
    {
        referenceBands ??= [];
        referenceBands.Add(new ReferenceBandConfig(from, to, label, axis, color));
        return (TSelf)this;
    }

    /// <summary>Adds an annotation marker at the specified data coordinates.</summary>
    public TSelf Annotation(
        object x,
        double y,
        string? label = null,
        Icon? icon = null,
        AnnotationAnchor anchor = AnnotationAnchor.Above)
    {
        annotations ??= [];
        annotations.Add(new AnnotationConfig(x, y, label, icon, anchor));
        return (TSelf)this;
    }

    /// <summary>Adds a trendline overlay to the chart.</summary>
    public TSelf Trendline(
        TrendlineType type,
        int? period = null,
        int? degree = null,
        ColorValue? color = null)
    {
        trendlineType = type;
        trendlinePeriod = period;
        trendlineDegree = degree;
        trendlineColor = color;
        return (TSelf)this;
    }

    /// <summary>Programmatically zooms to the specified X axis range.</summary>
    public void ZoomTo(object startX, object endX)
    {
        ArgumentNullException.ThrowIfNull(startX);
        ArgumentNullException.ThrowIfNull(endX);
        zoomStartX = startX;
        zoomEndX = endX;
    }

    /// <summary>Resets zoom and pan to the default view showing all data.</summary>
    public void ResetZoom()
    {
        zoomStartX = null;
        zoomEndX = null;
    }

    internal readonly record struct ReferenceLineConfig(
        double Value, string? Label, ChartAxis Axis, ColorValue? Color, LineStyle Style);

    internal readonly record struct ReferenceBandConfig(
        double From, double To, string? Label, ChartAxis Axis, ColorValue? Color);

    internal readonly record struct AnnotationConfig(
        object X, double Y, string? Label, Icon? Icon, AnnotationAnchor Anchor);
}

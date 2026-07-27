namespace Cascade.UI;

/// <summary>A single bar in a waterfall chart.</summary>
/// <param name="Label">The category label for this bar.</param>
/// <param name="Value">The numeric value (positive adds, negative subtracts for deltas).</param>
/// <param name="Type">Whether this item is a delta, subtotal, or total.</param>
public readonly record struct WaterfallItem(
    string Label,
    double Value,
    WaterfallItemType Type = WaterfallItemType.Delta);

/// <summary>Whether a waterfall item is an incremental change or a total/subtotal.</summary>
public enum WaterfallItemType
{
    /// <summary>An incremental change (positive adds, negative subtracts).</summary>
    Delta,

    /// <summary>A running subtotal bar that starts from zero.</summary>
    Subtotal,

    /// <summary>The final total bar that starts from zero.</summary>
    Total
}

/// <summary>
/// A waterfall chart that shows cumulative effect of sequential positive and negative values.
/// Each bar floats from its running total, with optional connector lines between bars
/// and distinct colors for positive, negative, and total items.
/// </summary>
public sealed class WaterfallChart : CartesianChart<WaterfallChart>
{
    internal List<WaterfallItem> itemsList = [];
    internal ColorValue? positiveColor;
    internal ColorValue? negativeColor;
    internal ColorValue? totalColor;
    internal bool showConnectors = true;
    internal LineStyle connectorStyle = LineStyle.Dashed;
    internal bool showValueLabels;
    internal AxisFormat? valueLabelFormat;

    /// <summary>Creates a waterfall chart from a sequence of items.</summary>
    /// <param name="data">The waterfall items defining each bar in the chart.</param>
    public WaterfallChart(IEnumerable<WaterfallItem> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        itemsList.AddRange(data);
    }

    /// <summary>The items in this chart.</summary>
    internal IReadOnlyList<WaterfallItem> Items => itemsList;

    /// <summary>Sets the colors for positive deltas, negative deltas, and total/subtotal bars.</summary>
    /// <param name="positive">Color for bars with positive delta values.</param>
    /// <param name="negative">Color for bars with negative delta values.</param>
    /// <param name="total">Color for total and subtotal bars.</param>
    public WaterfallChart Colors(ColorValue positive, ColorValue negative, ColorValue total)
    {
        positiveColor = positive;
        negativeColor = negative;
        totalColor = total;
        return this;
    }

    /// <summary>Configures the connector lines drawn between consecutive bars.</summary>
    /// <param name="show">True to draw connector lines between bars.</param>
    /// <param name="style">The line style for connectors.</param>
    public WaterfallChart Connectors(bool show, LineStyle style = LineStyle.Dashed)
    {
        showConnectors = show;
        connectorStyle = style;
        return this;
    }

    /// <summary>Configures whether value labels are displayed on each bar.</summary>
    /// <param name="show">True to show value labels.</param>
    /// <param name="format">Optional format for the displayed values.</param>
    public WaterfallChart ValueLabels(bool show, AxisFormat? format = null)
    {
        showValueLabels = show;
        valueLabelFormat = format;
        return this;
    }
}

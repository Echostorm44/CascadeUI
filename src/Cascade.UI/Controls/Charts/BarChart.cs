namespace Cascade.UI;

/// <summary>
/// A bar/column chart that renders data as vertical or horizontal bars.
/// Supports grouped, stacked, and stacked-percent modes for multi-series data.
/// The control is named BarChart regardless of orientation.
/// </summary>
public sealed class BarChart : CartesianChart<BarChart>
{
    internal List<ChartSeries> seriesList = [];
    internal ChartOrientation orientationValue = ChartOrientation.Vertical;
    internal BarGroupMode groupModeValue = BarGroupMode.Grouped;
    internal float barWidthFraction = 0.7f;

    /// <summary>Creates a single-series bar chart from labeled X/Y data points.</summary>
    /// <param name="data">The data points with arbitrary-typed X (categories) and numeric Y values.</param>
    public BarChart(IEnumerable<(object X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
    }

    /// <summary>Creates a single-series bar chart from Y values with array indices as categories.</summary>
    /// <param name="data">The Y values; categories are inferred from array indices.</param>
    public BarChart(IEnumerable<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
    }

    /// <summary>Creates a multi-series bar chart.</summary>
    /// <param name="series">The data series to plot.</param>
    public BarChart(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        seriesList.AddRange(series);
    }

    /// <summary>The data series in this chart.</summary>
    internal IReadOnlyList<ChartSeries> Series => seriesList;

    /// <summary>Sets vertical (column) or horizontal (bar) orientation. Default is vertical.</summary>
    /// <param name="orientation">The bar orientation.</param>
    public BarChart Orientation(ChartOrientation orientation)
    {
        orientationValue = orientation;
        return this;
    }

    /// <summary>Sets the bar grouping mode for multi-series data.</summary>
    /// <param name="mode">How bars from different series are arranged.</param>
    public BarChart GroupMode(BarGroupMode mode)
    {
        groupModeValue = mode;
        return this;
    }

    /// <summary>Sets the bar width as a fraction of the available category slot. Default is 0.7.</summary>
    /// <param name="fraction">Width fraction from 0.0 to 1.0.</param>
    public BarChart BarWidth(float fraction)
    {
        barWidthFraction = Math.Clamp(fraction, 0f, 1f);
        return this;
    }
}

namespace Cascade.UI;

/// <summary>
/// A line chart that plots one or more data series as connected lines
/// with optional smooth interpolation, data point markers, and missing value handling.
/// </summary>
public sealed class LineChart : CartesianChart<LineChart>
{
    internal List<ChartSeries> seriesList = [];
    internal bool smoothEnabled;
    internal PointDisplay pointDisplay = PointDisplay.Auto;
    internal MissingValueMode missingValueMode = MissingValueMode.Gap;

    /// <summary>Creates a single-series line chart from labeled X/Y data points.</summary>
    /// <param name="data">The data points with arbitrary-typed X and numeric Y values.</param>
    public LineChart(IEnumerable<(object X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
    }

    /// <summary>Creates a single-series line chart from Y values with array indices as X values.</summary>
    /// <param name="data">The Y values; X values are inferred from array indices.</param>
    public LineChart(IEnumerable<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
    }

    /// <summary>Creates a multi-series line chart.</summary>
    /// <param name="series">The data series to plot.</param>
    public LineChart(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        seriesList.AddRange(series);
    }

    /// <summary>The data series in this chart.</summary>
    internal IReadOnlyList<ChartSeries> Series => seriesList;

    /// <summary>Enables or disables Catmull-Rom smooth interpolation between data points.</summary>
    /// <param name="smooth">True for smooth curves, false for straight line segments.</param>
    public LineChart Smooth(bool smooth)
    {
        smoothEnabled = smooth;
        return this;
    }

    /// <summary>Controls when data point markers are displayed on the line.</summary>
    /// <param name="display">The point display strategy.</param>
    public LineChart Points(PointDisplay display)
    {
        pointDisplay = display;
        return this;
    }

    /// <summary>Controls how null or missing values in the data are handled.</summary>
    /// <param name="mode">The missing value handling mode.</param>
    public LineChart MissingValues(MissingValueMode mode)
    {
        missingValueMode = mode;
        return this;
    }
}

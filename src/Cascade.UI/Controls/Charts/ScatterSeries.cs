namespace Cascade.UI;

/// <summary>
/// A named data series for use in scatter plots and bubble charts.
/// Constructed with a name and coordinate data, then optionally configured with fluent modifiers.
/// </summary>
public sealed class ScatterSeries
{
    internal List<(double X, double Y)> dataPointsList = [];
    internal List<(double X, double Y, double Size)>? bubbleDataPointsList;
    internal ColorValue? colorOverride;
    internal bool hiddenState;

    /// <summary>Creates a scatter series with X/Y coordinate pairs.</summary>
    /// <param name="name">The display name of the series.</param>
    /// <param name="data">The X/Y coordinate data points.</param>
    public ScatterSeries(string name, IEnumerable<(double X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        SeriesName = name;
        dataPointsList.AddRange(data);
    }

    /// <summary>Creates a bubble series with X/Y coordinates and a size dimension.</summary>
    /// <param name="name">The display name of the series.</param>
    /// <param name="data">The X/Y/Size data points for bubble chart rendering.</param>
    public ScatterSeries(string name, IEnumerable<(double X, double Y, double Size)> data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        SeriesName = name;
        bubbleDataPointsList = [.. data];
        foreach (var point in bubbleDataPointsList)
        {
            dataPointsList.Add((point.X, point.Y));
        }
    }

    /// <summary>The display name of this series.</summary>
    public string SeriesName { get; }

    /// <summary>The data points in this series.</summary>
    internal IReadOnlyList<(double X, double Y)> DataPoints => dataPointsList;

    /// <summary>The bubble data points with size dimension, if any.</summary>
    internal IReadOnlyList<(double X, double Y, double Size)>? BubbleDataPoints => bubbleDataPointsList;

    /// <summary>Overrides the palette color for this series.</summary>
    public ScatterSeries Color(ColorValue color)
    {
        colorOverride = color;
        return this;
    }

    /// <summary>Hides or shows this series. Hidden series are dimmed in the legend.</summary>
    public ScatterSeries Hidden(bool hidden = true)
    {
        this.hiddenState = hidden;
        return this;
    }
}

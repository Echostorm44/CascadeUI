namespace Cascade.UI;

/// <summary>
/// A scatter plot that renders data points as positioned markers.
/// Supports multiple series, bubble size variant (third dimension mapped to point size),
/// opacity control, and jitter for reducing overplotting.
/// </summary>
public sealed class ScatterPlot : CartesianChart<ScatterPlot>
{
    internal List<ScatterSeries> seriesList = [];
    internal float pointRadiusValue = 4f;
    internal float pointOpacityValue = 1f;
    internal float jitterAmount;
    internal float bubbleMinRadius = 4f;
    internal float bubbleMaxRadius = 24f;
    internal AxisFormat? bubbleSizeFormat;
    internal bool bubbleEnabled;

    /// <summary>Creates a single-series scatter plot from X/Y coordinate pairs.</summary>
    /// <param name="data">The X/Y coordinate data points.</param>
    public ScatterPlot(IEnumerable<(double X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ScatterSeries("Series 1", data));
    }

    /// <summary>Creates a multi-series scatter plot.</summary>
    /// <param name="series">The scatter series to plot.</param>
    public ScatterPlot(IReadOnlyList<ScatterSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        seriesList.AddRange(series);
    }

    /// <summary>The scatter series in this chart.</summary>
    internal IReadOnlyList<ScatterSeries> Series => seriesList;

    /// <summary>Sets the base radius of data point markers in logical pixels.</summary>
    /// <param name="radius">The point marker radius.</param>
    public ScatterPlot PointRadius(float radius)
    {
        pointRadiusValue = radius;
        return this;
    }

    /// <summary>Sets the opacity of data points. Useful when many points overlap.</summary>
    /// <param name="opacity">Opacity from 0.0 (transparent) to 1.0 (opaque).</param>
    public ScatterPlot PointOpacity(float opacity)
    {
        pointOpacityValue = Math.Clamp(opacity, 0f, 1f);
        return this;
    }

    /// <summary>Adds random positional offset to points to reduce overplotting.</summary>
    /// <param name="amount">The jitter amount in logical pixels.</param>
    public ScatterPlot Jitter(float amount)
    {
        jitterAmount = amount;
        return this;
    }

    /// <summary>Enables bubble chart mode with point sizes mapped to a third data dimension.</summary>
    /// <param name="min">Minimum bubble radius in logical pixels.</param>
    /// <param name="max">Maximum bubble radius in logical pixels.</param>
    /// <param name="format">Optional format for the size value shown in tooltips.</param>
    public ScatterPlot BubbleSize(float min = 4, float max = 24, AxisFormat? format = null)
    {
        bubbleEnabled = true;
        bubbleMinRadius = min;
        bubbleMaxRadius = max;
        bubbleSizeFormat = format;
        return this;
    }
}

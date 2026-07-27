namespace Cascade.UI;

/// <summary>
/// A named data series for use in line, area, bar, and combo charts.
/// Constructed with a name and data, then optionally configured with fluent modifiers.
/// </summary>
public sealed class ChartSeries
{
    internal List<(object X, double Y)> dataPointsList = [];
    internal ColorValue? colorOverride;
    internal ChartAxis yAxisValue = ChartAxis.Y;
    internal LineStyle lineStyleValue = Cascade.UI.LineStyle.Solid;
    internal float lineWidthValue = 2f;
    internal PointShape pointShapeValue = Cascade.UI.PointShape.Circle;
    internal bool smoothEnabled;
    internal TrendlineType? trendlineType;
    internal ColorValue? trendlineColor;
    internal bool hiddenState;

    /// <summary>Creates a series with labeled X values and numeric Y values.</summary>
    /// <param name="name">The display name of the series (shown in legends and tooltips).</param>
    /// <param name="data">The data points with arbitrary-typed X and numeric Y values.</param>
    public ChartSeries(string name, IEnumerable<(object X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        SeriesName = name;
        dataPointsList.AddRange(data);
    }

    /// <summary>Creates a series using array indices as X values.</summary>
    /// <param name="name">The display name of the series.</param>
    /// <param name="data">The Y values; X values are inferred from array indices.</param>
    public ChartSeries(string name, IEnumerable<double> data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        SeriesName = name;
        int index = 0;
        foreach (var y in data)
        {
            dataPointsList.Add(((object)index, y));
            index++;
        }
    }

    /// <summary>The display name of this series.</summary>
    public string SeriesName { get; private set; }

    /// <summary>The data points in this series.</summary>
    internal IReadOnlyList<(object X, double Y)> DataPoints => dataPointsList;

    /// <summary>Overrides the palette color for this series.</summary>
    public ChartSeries Color(ColorValue color)
    {
        colorOverride = color;
        return this;
    }

    /// <summary>Binds this series to the primary or secondary Y axis.</summary>
    public ChartSeries YAxis(ChartAxis axis)
    {
        yAxisValue = axis;
        return this;
    }

    /// <summary>Sets the line stroke style for this series in line and area charts.</summary>
    public ChartSeries LineStyle(LineStyle style)
    {
        lineStyleValue = style;
        return this;
    }

    /// <summary>Sets the line width for this series.</summary>
    public ChartSeries LineWidth(float width)
    {
        lineWidthValue = width;
        return this;
    }

    /// <summary>Sets the data point marker shape for this series.</summary>
    public ChartSeries PointShape(PointShape shape)
    {
        pointShapeValue = shape;
        return this;
    }

    /// <summary>Enables or disables smooth Catmull-Rom interpolation for this series.</summary>
    public ChartSeries Smooth(bool smooth)
    {
        this.smoothEnabled = smooth;
        return this;
    }

    /// <summary>Adds a trendline overlay for this series.</summary>
    /// <param name="type">The type of trendline to compute.</param>
    /// <param name="color">Optional color override for the trendline. Defaults to a muted variant of the series color.</param>
    public ChartSeries Trendline(TrendlineType type, ColorValue? color = null)
    {
        trendlineType = type;
        trendlineColor = color;
        return this;
    }

    /// <summary>Hides or shows this series. Hidden series are dimmed in the legend.</summary>
    public ChartSeries Hidden(bool hidden = true)
    {
        this.hiddenState = hidden;
        return this;
    }

    /// <summary>Overrides the display name of this series.</summary>
    public ChartSeries Name(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        SeriesName = name;
        return this;
    }
}

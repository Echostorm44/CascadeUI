namespace Cascade.UI;

/// <summary>
/// A pie chart that renders data as proportional slices of a circle.
/// Supports donut variant with center label, slice explosion, sorting,
/// and configurable start angle.
/// </summary>
public sealed class PieChart : ChartBase<PieChart>
{
    internal List<PieSlice> slicesList = [];
    internal float holeRadiusValue;
    internal double? donutLabelValue;
    internal AxisFormat? donutLabelFormat;
    internal string? donutSubLabel;
    internal Angle startAngleValue = Angle.Degrees(-90);
    internal bool sortSlicesEnabled;
    internal Angle? minSliceAngleValue;
    internal bool dataLabelsEnabledValue;
    internal DataLabelPosition dataLabelPositionValue = DataLabelPosition.Auto;
    internal AxisFormat? dataLabelFormatValue;
    internal DataLabelOverlap dataLabelOverlapValue = DataLabelOverlap.Hide;

    /// <summary>Creates a pie chart from labeled value pairs.</summary>
    /// <param name="data">The label/value pairs. The chart computes percentages from raw values.</param>
    public PieChart(IEnumerable<(string Label, double Value)> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var (label, value) in data)
        {
            slicesList.Add(new PieSlice(label, value));
        }
    }

    /// <summary>Creates a pie chart from explicitly defined slices.</summary>
    /// <param name="series">The pie slices to render.</param>
    public PieChart(IReadOnlyList<PieSlice> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        slicesList.AddRange(series);
    }

    /// <summary>The slices in this chart.</summary>
    internal IReadOnlyList<PieSlice> Slices => slicesList;

    /// <summary>Converts to a donut chart with the specified hole radius.</summary>
    /// <param name="holeRadius">Hole size from 0.0 (solid pie) to 1.0 (no visible ring).</param>
    public PieChart Donut(float holeRadius = 0.6f)
    {
        holeRadiusValue = Math.Clamp(holeRadius, 0f, 1f);
        return this;
    }

    /// <summary>Sets the center label displayed in the donut hole.</summary>
    /// <param name="value">The numeric value to display.</param>
    /// <param name="format">How the value is formatted.</param>
    /// <param name="subLabel">Optional secondary label below the value (e.g., "Total Revenue").</param>
    public PieChart DonutLabel(double value, AxisFormat? format = null, string? subLabel = null)
    {
        donutLabelValue = value;
        donutLabelFormat = format;
        donutSubLabel = subLabel;
        return this;
    }

    /// <summary>Sets the starting angle for the first slice. Default is 12 o'clock (-90°).</summary>
    /// <param name="angle">The angle at which the first slice begins.</param>
    public PieChart StartAngle(Angle angle)
    {
        startAngleValue = angle;
        return this;
    }

    /// <summary>Sorts slices by value, largest first. Default is false (preserves data order).</summary>
    /// <param name="sort">True to sort by descending value.</param>
    public PieChart SortSlices(bool sort)
    {
        sortSlicesEnabled = sort;
        return this;
    }

    /// <summary>Sets the minimum slice angle. Slices smaller than this are grouped into "Other".</summary>
    /// <param name="angle">The minimum angle threshold.</param>
    public PieChart MinSliceAngle(Angle angle)
    {
        minSliceAngleValue = angle;
        return this;
    }

    /// <summary>Configures data labels displayed on or near pie slices.</summary>
    /// <param name="enabled">Whether data labels are shown.</param>
    /// <param name="position">Where labels are placed relative to slices.</param>
    /// <param name="format">How label values are formatted.</param>
    /// <param name="overlap">How overlapping labels are handled.</param>
    public PieChart DataLabels(
        bool enabled = true,
        DataLabelPosition position = DataLabelPosition.Auto,
        AxisFormat? format = null,
        DataLabelOverlap overlap = DataLabelOverlap.Hide)
    {
        dataLabelsEnabledValue = enabled;
        dataLabelPositionValue = position;
        dataLabelFormatValue = format;
        dataLabelOverlapValue = overlap;
        return this;
    }
}

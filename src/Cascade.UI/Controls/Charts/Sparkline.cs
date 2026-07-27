namespace Cascade.UI;

/// <summary>
/// A minimal inline chart for dashboards, table cells, and stat cards.
/// No axes, no legend, no tooltip by default — just the data shape.
/// Sparklines are not focusable unless explicitly made accessible.
/// </summary>
public sealed class Sparkline : Node
{
    internal List<double> dataValues = [];
    internal SparklineType typeValue = SparklineType.Line;
    internal float widthValue = 80f;
    internal float heightValue = 24f;
    internal ColorValue? colorValue;
    internal ColorValue? negativeColorValue;
    internal double? normalBandLower;
    internal double? normalBandUpper;
    internal string? accessibleText;

    /// <summary>Creates a sparkline from a sequence of numeric values.</summary>
    /// <param name="data">The data values to visualize.</param>
    public Sparkline(IEnumerable<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        dataValues.AddRange(data);
    }

    /// <summary>The data values in this sparkline.</summary>
    internal IReadOnlyList<double> DataPoints => dataValues;

    /// <summary>Sets the sparkline visual type. Default is Line.</summary>
    /// <param name="type">The sparkline rendering type.</param>
    public Sparkline Type(SparklineType type)
    {
        typeValue = type;
        return this;
    }

    /// <summary>Sets the width in logical pixels.</summary>
    /// <param name="width">The sparkline width.</param>
    public Sparkline Width(float width)
    {
        widthValue = width;
        return this;
    }

    /// <summary>Sets the height in logical pixels.</summary>
    /// <param name="height">The sparkline height.</param>
    public Sparkline Height(float height)
    {
        heightValue = height;
        return this;
    }

    /// <summary>Sets the primary color for the sparkline stroke or bars.</summary>
    /// <param name="color">The primary color.</param>
    public Sparkline Color(ColorValue color)
    {
        colorValue = color;
        return this;
    }

    /// <summary>Sets the color for negative values in Bar and WinLoss types.</summary>
    /// <param name="color">The color applied to negative data points.</param>
    public Sparkline NegativeColor(ColorValue color)
    {
        negativeColorValue = color;
        return this;
    }

    /// <summary>Adds a shaded reference band showing a normal value range.</summary>
    /// <param name="lower">The lower bound of the normal range.</param>
    /// <param name="upper">The upper bound of the normal range.</param>
    public Sparkline NormalBand(double lower, double upper)
    {
        normalBandLower = lower;
        normalBandUpper = upper;
        return this;
    }

    /// <summary>Adds the sparkline to the accessibility tree with a text summary for screen readers.</summary>
    /// <param name="summary">The descriptive text for the sparkline data (e.g., "Weekly trend: up 12%").</param>
    public Sparkline Accessible(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        accessibleText = summary;
        return this;
    }
}

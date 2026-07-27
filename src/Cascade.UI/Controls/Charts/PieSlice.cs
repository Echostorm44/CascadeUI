namespace Cascade.UI;

/// <summary>
/// A single slice in a <see cref="PieChart"/>, with a label and raw value.
/// Percentages are computed by the chart — do not pass pre-computed percentages.
/// </summary>
public sealed class PieSlice
{
    internal float explodedOffset;
    internal ColorValue? colorOverride;

    /// <summary>Creates a pie slice with the given label and raw value.</summary>
    /// <param name="label">The display label for this slice.</param>
    /// <param name="value">The raw numeric value. The chart computes the percentage.</param>
    public PieSlice(string label, double value)
    {
        ArgumentNullException.ThrowIfNull(label);
        Label = label;
        Value = value;
    }

    /// <summary>The display label for this slice.</summary>
    public string Label { get; }

    /// <summary>The raw numeric value for this slice.</summary>
    public double Value { get; }

    /// <summary>Pulls this slice outward from the center by the specified offset in logical pixels.</summary>
    /// <param name="offset">Distance to pull the slice outward.</param>
    public PieSlice Exploded(float offset = 8)
    {
        explodedOffset = offset;
        return this;
    }

    /// <summary>Overrides the palette color for this slice.</summary>
    public PieSlice Color(ColorValue color)
    {
        colorOverride = color;
        return this;
    }
}

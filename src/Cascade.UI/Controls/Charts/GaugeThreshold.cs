namespace Cascade.UI;

/// <summary>
/// A threshold boundary for a <see cref="DonutGauge"/> that changes the fill color
/// when the gauge value is at or above the specified value.
/// </summary>
public sealed class GaugeThreshold
{
    /// <summary>Creates a threshold at the specified value with the given color.</summary>
    /// <param name="value">The threshold value. When the gauge value meets or exceeds this, the color applies.</param>
    /// <param name="color">The fill color applied when this threshold is active.</param>
    public GaugeThreshold(float value, ColorValue color)
    {
        Value = value;
        Color = color;
    }

    /// <summary>The threshold value boundary.</summary>
    public float Value { get; }

    /// <summary>The fill color when the gauge value is at or above this threshold.</summary>
    public ColorValue Color { get; }
}

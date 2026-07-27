namespace Cascade.UI;

/// <summary>
/// A single-value donut/ring gauge for KPI display.
/// Not a multi-series chart — one value, one ring with optional color thresholds.
/// Animates by sweeping from zero to the current value on entry.
/// </summary>
public sealed class DonutGauge : Node
{
    internal float gaugeValue;
    internal Bindable<float>? bindableValue;
    internal float sizeValue = 120f;
    internal float thicknessValue = 12f;
    internal GaugeFormat? formatValue;
    internal string? labelText;
    internal ColorValue? colorValue;
    internal ColorValue? trackColorValue;
    // A donut is a closed ring: fill clockwise from the top (12 o'clock). Use the
    // separate Gauge control for speedometer-style partial arcs; override
    // StartAngle/SweepAngle here if a partial donut arc is genuinely wanted.
    internal Angle startAngleValue = Angle.Degrees(-90);
    internal Angle sweepAngleValue = Angle.Degrees(360);
    internal IReadOnlyList<GaugeThreshold>? thresholdList;
    internal AnimateTrigger animateTriggerValue = AnimateTrigger.Load;
    internal string? accessibleSummaryText;

    /// <summary>Creates a donut gauge displaying the specified value.</summary>
    /// <param name="value">The gauge value, typically 0.0 to 1.0 for percentage display.</param>
    public DonutGauge(float value)
    {
        gaugeValue = value;
    }

    /// <summary>Creates a donut gauge with a reactive value binding.</summary>
    /// <param name="value">A bindable value that updates the gauge reactively.</param>
    public DonutGauge(Bindable<float> value)
    {
        bindableValue = value;
        gaugeValue = value.Value;
    }

    /// <summary>Sets the overall size of the gauge in logical pixels.</summary>
    /// <param name="size">The diameter of the gauge ring.</param>
    public DonutGauge Size(float size)
    {
        sizeValue = size;
        return this;
    }

    /// <summary>Sets the ring thickness in logical pixels.</summary>
    /// <param name="thickness">The width of the gauge ring stroke.</param>
    public DonutGauge Thickness(float thickness)
    {
        thicknessValue = thickness;
        return this;
    }

    /// <summary>Sets the value display format in the center label.</summary>
    /// <param name="format">The gauge value format.</param>
    public DonutGauge Format(GaugeFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        formatValue = format;
        return this;
    }

    /// <summary>Sets a descriptive label displayed below the center value.</summary>
    /// <param name="label">The descriptive text (e.g., "Completion").</param>
    public DonutGauge Label(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        labelText = label;
        return this;
    }

    /// <summary>Sets the fill color for the active arc.</summary>
    /// <param name="color">The arc fill color.</param>
    public DonutGauge Color(ColorValue color)
    {
        colorValue = color;
        return this;
    }

    /// <summary>Sets the background track color for the unfilled portion of the ring.</summary>
    /// <param name="color">The track color.</param>
    public DonutGauge TrackColor(ColorValue color)
    {
        trackColorValue = color;
        return this;
    }

    /// <summary>Sets the starting angle of the gauge arc. Default is -90° (top).</summary>
    /// <param name="angle">The angle at which the arc begins.</param>
    public DonutGauge StartAngle(Angle angle)
    {
        startAngleValue = angle;
        return this;
    }

    /// <summary>Sets the total sweep angle of the gauge arc. Default is 360° (full ring).</summary>
    /// <param name="angle">The total arc sweep.</param>
    public DonutGauge SweepAngle(Angle angle)
    {
        sweepAngleValue = angle;
        return this;
    }

    /// <summary>Adds color thresholds that change the fill color at specified value boundaries.</summary>
    /// <param name="thresholds">Ordered thresholds from lowest to highest value.</param>
    public DonutGauge Thresholds(IReadOnlyList<GaugeThreshold> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholdList = thresholds;
        return this;
    }

    /// <summary>Configures the animation trigger for the gauge.</summary>
    /// <param name="on">The animation trigger mode.</param>
    public DonutGauge Animate(AnimateTrigger on = AnimateTrigger.Load)
    {
        animateTriggerValue = on;
        return this;
    }

    /// <summary>Provides a custom accessibility summary for screen readers.</summary>
    /// <param name="summary">The text summary describing the gauge value.</param>
    public DonutGauge AccessibleSummary(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        accessibleSummaryText = summary;
        return this;
    }
}

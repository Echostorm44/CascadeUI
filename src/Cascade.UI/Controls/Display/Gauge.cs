namespace Cascade.UI;

/// <summary>
/// A circular or semi-circular gauge for displaying a single value within
/// a range — CPU usage, speed, temperature, completion percentage, etc.
/// </summary>
public sealed class Gauge : Node
{
    /// <summary>
    /// Creates a gauge.
    /// </summary>
    /// <param name="value">Current value.</param>
    /// <param name="min">Minimum value of the range.</param>
    /// <param name="max">Maximum value of the range.</param>
    /// <param name="style">Gauge visual style.</param>
    public Gauge(
        float value,
        float min = 0f,
        float max = 1f,
        GaugeStyle style = GaugeStyle.Full)
    {
        Value = value;
        Min = min;
        Max = max;
        GaugeDisplayStyle = style;
    }

    /// <summary>Current value.</summary>
    public float Value { get; }

    /// <summary>Minimum value of the range.</summary>
    public float Min { get; }

    /// <summary>Maximum value of the range.</summary>
    public float Max { get; }

    /// <summary>Gauge visual style.</summary>
    public GaugeStyle GaugeDisplayStyle { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ColorValue? FillColorOverride { get; set; }
    internal ColorValue? TrackColorOverride { get; set; }
    internal float? StrokeWidthOverride { get; set; }
    internal bool ShowValueEnabled { get; set; }
    internal string? ValueFormat { get; set; }
    internal string? CenterLabel { get; set; }
    internal IReadOnlyList<GaugeSegment> SegmentRanges { get; set; } = Array.Empty<GaugeSegment>();
    internal bool AnimatedEnabled { get; set; } = true;

    /// <summary>Sets the fill color.</summary>
    public Gauge FillColor(ColorValue color)
    {
        FillColorOverride = color;
        return this;
    }

    /// <summary>Sets the track color.</summary>
    public Gauge TrackColor(ColorValue color)
    {
        TrackColorOverride = color;
        return this;
    }

    /// <summary>Sets the stroke width of the gauge arc.</summary>
    public Gauge StrokeWidth(float width)
    {
        StrokeWidthOverride = width;
        return this;
    }

    /// <summary>Shows the current value as a label in the center.</summary>
    public Gauge ShowValue(bool enabled, string? format = null)
    {
        ShowValueEnabled = enabled;
        ValueFormat = format;
        return this;
    }

    /// <summary>Sets a custom label displayed in the center of the gauge.</summary>
    public Gauge Label(string text)
    {
        CenterLabel = text;
        return this;
    }

    /// <summary>Sets color segments for different value ranges.</summary>
    public Gauge Segments(IReadOnlyList<GaugeSegment> segments)
    {
        SegmentRanges = segments;
        return this;
    }

    /// <summary>Enables or disables the animated value transition.</summary>
    public Gauge Animated(bool enabled)
    {
        AnimatedEnabled = enabled;
        return this;
    }
}

/// <summary>
/// Visual style variant for a <see cref="Gauge"/>.
/// </summary>
public enum GaugeStyle
{
    /// <summary>Full 360° circle.</summary>
    Full,

    /// <summary>Semi-circle (180° arc).</summary>
    Semi,

    /// <summary>Quarter circle (90° arc).</summary>
    Quarter
}

/// <summary>
/// A color segment within a <see cref="Gauge"/> for multi-zone display.
/// </summary>
public sealed class GaugeSegment
{
    /// <summary>Creates a gauge segment.</summary>
    /// <param name="from">Start value (inclusive).</param>
    /// <param name="to">End value (inclusive).</param>
    /// <param name="color">Segment color.</param>
    public GaugeSegment(float from, float to, ColorValue color)
    {
        From = from;
        To = to;
        Color = color;
    }

    /// <summary>Start value (inclusive).</summary>
    public float From { get; }

    /// <summary>End value (inclusive).</summary>
    public float To { get; }

    /// <summary>Segment color.</summary>
    public ColorValue Color { get; }
}

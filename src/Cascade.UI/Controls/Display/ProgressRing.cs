namespace Cascade.UI;

/// <summary>
/// A circular progress indicator showing completion as a filled arc.
/// Supports both determinate (value-based) and indeterminate (animated) modes.
/// Complements the linear <see cref="ProgressBar"/>.
/// </summary>
public sealed class ProgressRing : Node
{
    /// <summary>
    /// Creates a determinate progress ring.
    /// </summary>
    /// <param name="value">Current progress from 0.0 to 1.0.</param>
    public ProgressRing(float value)
    {
        Value = Math.Clamp(value, 0f, 1f);
        Mode = ProgressMode.Determinate;
    }

    /// <summary>
    /// Creates a progress ring with an explicit mode.
    /// </summary>
    /// <param name="mode">Determinate or indeterminate.</param>
    /// <param name="value">Current progress (ignored in indeterminate mode).</param>
    public ProgressRing(ProgressMode mode, float value = 0f)
    {
        Value = Math.Clamp(value, 0f, 1f);
        Mode = mode;
    }

    /// <summary>Current progress from 0.0 to 1.0.</summary>
    public float Value { get; }

    /// <summary>Determinate or indeterminate mode.</summary>
    public ProgressMode Mode { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal float? SizeOverride { get; set; }
    internal float? StrokeWidthOverride { get; set; }
    internal ColorValue? FillColorOverride { get; set; }
    internal ColorValue? TrackColorOverride { get; set; }
    internal bool ShowValueEnabled { get; set; }
    internal Func<float, string>? LabelFormatter { get; set; }

    /// <summary>Sets the ring diameter in logical pixels.</summary>
    public ProgressRing Size(float size)
    {
        SizeOverride = size;
        return this;
    }

    /// <summary>Sets the ring stroke width.</summary>
    public ProgressRing StrokeWidth(float width)
    {
        StrokeWidthOverride = width;
        return this;
    }

    /// <summary>Sets the fill arc color, overriding the theme default.</summary>
    public ProgressRing FillColor(ColorValue color)
    {
        FillColorOverride = color;
        return this;
    }

    /// <summary>Sets the track ring color, overriding the theme default.</summary>
    public ProgressRing TrackColor(ColorValue color)
    {
        TrackColorOverride = color;
        return this;
    }

    /// <summary>Shows the progress percentage in the center.</summary>
    public ProgressRing ShowValue(bool enabled = true)
    {
        ShowValueEnabled = enabled;
        return this;
    }

    /// <summary>Sets a custom label format for the center text.</summary>
    public ProgressRing LabelFormat(Func<float, string> formatter)
    {
        LabelFormatter = formatter;
        return this;
    }
}

namespace Cascade.UI;

/// <summary>
/// A linear progress indicator supporting both determinate (known progress)
/// and indeterminate (unknown duration) modes. Themed via
/// <see cref="ProgressTheme"/>.
/// </summary>
public sealed class ProgressBar : Node
{
    /// <summary>
    /// Creates a determinate progress bar.
    /// </summary>
    /// <param name="value">Current progress from 0.0 to 1.0.</param>
    public ProgressBar(float value)
    {
        Value = value;
        Mode = ProgressMode.Determinate;
    }

    /// <summary>
    /// Creates a progress bar with an explicit mode.
    /// </summary>
    /// <param name="mode">Determinate or indeterminate.</param>
    /// <param name="value">Current progress (ignored in indeterminate mode).</param>
    public ProgressBar(ProgressMode mode, float value = 0f)
    {
        Value = value;
        Mode = mode;
    }

    /// <summary>Current progress from 0.0 to 1.0.</summary>
    public float Value { get; }

    /// <summary>Determinate or indeterminate mode.</summary>
    public ProgressMode Mode { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ColorValue? FillColorOverride { get; set; }
    internal ColorValue? TrackColorOverride { get; set; }
    internal float? HeightOverride { get; set; }
    internal bool ShowLabelEnabled { get; set; }
    internal Func<float, string>? LabelFormatter { get; set; }
    internal bool AnimatedEnabled { get; set; } = true;

    /// <summary>Sets the fill color, overriding the theme default.</summary>
    public ProgressBar FillColor(ColorValue color)
    {
        FillColorOverride = color;
        return this;
    }

    /// <summary>Sets the track color, overriding the theme default.</summary>
    public ProgressBar TrackColor(ColorValue color)
    {
        TrackColorOverride = color;
        return this;
    }

    /// <summary>Sets the bar height, overriding the theme default.</summary>
    public ProgressBar Height(float height)
    {
        HeightOverride = height;
        return this;
    }

    /// <summary>Shows a percentage label on or beside the bar.</summary>
    public ProgressBar ShowLabel(bool enabled)
    {
        ShowLabelEnabled = enabled;
        return this;
    }

    /// <summary>Sets a custom label format.</summary>
    public ProgressBar LabelFormat(Func<float, string> formatter)
    {
        LabelFormatter = formatter;
        return this;
    }

    /// <summary>Enables or disables the animated value transition.</summary>
    public ProgressBar Animated(bool enabled)
    {
        AnimatedEnabled = enabled;
        return this;
    }
}

/// <summary>
/// Progress indicator mode.
/// </summary>
public enum ProgressMode
{
    /// <summary>Known progress — shows a filled portion based on value.</summary>
    Determinate,

    /// <summary>Unknown duration — shows an animated repeating indicator.</summary>
    Indeterminate
}

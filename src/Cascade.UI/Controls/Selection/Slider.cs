namespace Cascade.UI;

/// <summary>
/// A single-value slider control for selecting a numeric value within a range.
/// Supports step snapping, tick marks, value labels, and custom formatting.
/// </summary>
public sealed class Slider : Node, IValidatable
{
    /// <summary>
    /// Creates a slider bound to a float field.
    /// </summary>
    /// <param name="bind">Two-way binding to the slider value.</param>
    /// <param name="min">Minimum track value.</param>
    /// <param name="max">Maximum track value.</param>
    /// <param name="step">Step increment for snapping. Null for continuous.</param>
    /// <param name="label">Optional label displayed above the slider.</param>
    public Slider(
        Bindable<float> bind,
        float min = 0f,
        float max = 1f,
        float? step = null,
        LocKey label = default)
    {
        Bind = bind;
        Min = min;
        Max = max;
        Step = step;
        Label = label;
    }

    /// <summary>Two-way binding to the slider value.</summary>
    public Bindable<float> Bind { get; }

    /// <summary>Minimum track value.</summary>
    public float Min { get; }

    /// <summary>Maximum track value.</summary>
    public float Max { get; }

    /// <summary>Step increment for snapping. Null for continuous.</summary>
    public float? Step { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal string? FormatString { get; set; }
    internal bool ShowValueLabelValue { get; set; }
    internal bool ShowTicksValue { get; set; }
    internal Orientation OrientationValue { get; set; } = Orientation.Horizontal;
    internal float? WidthValue { get; set; }
    internal List<Func<float, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;

    // ── Validation (see Validation) ──
    ValidationTrigger IValidatable.ValidationTriggerMode => ValidationTriggerMode;
    bool IValidatable.HasValidationRules => ValidationRules.Count > 0;
    ValidationResult IValidatable.RunValidation() => RunValidation();
    object? IValidatable.ValidationValue => Bind.Value;
    TimeSpan? IValidatable.ValidationDebounce => null;
    ValidationState? IValidatable.ValidationState { get; set; }
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    /// <summary>Window-space bounds from the last paint, for pointer math.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Room tick marks take beyond the track's usual 32px band.</summary>
    internal const float TickSpace = 8f;

    /// <summary>The track's rectangle, and where tick marks start across the control (y, or x when vertical).</summary>
    internal readonly record struct SliderGeometry(Rect Track, float TickLine);

    /// <summary>
    /// The track within <paramref name="bounds"/>: centred across a 32px band (tick marks sit beyond
    /// it, below or to the right), inset by half a thumb at each end so the thumb's centre reaches
    /// both. Shared by paint and pointer input so a press lands where the thumb is drawn.
    /// </summary>
    internal static SliderGeometry Geometry(bool vertical, bool ticks, Rect bounds, float thumbAlong, float trackThickness)
    {
        float tickSpace = ticks ? TickSpace : 0f;
        if (vertical)
        {
            float band = bounds.Width - tickSpace;
            float x = bounds.X + (band - trackThickness) / 2f;
            var track = new Rect(x, bounds.Y + thumbAlong / 2f, trackThickness, Math.Max(0f, bounds.Height - thumbAlong));
            return new SliderGeometry(track, bounds.X + band - 2f);
        }
        else
        {
            float band = bounds.Height - tickSpace;
            float y = bounds.Y + (band - trackThickness) / 2f;
            var track = new Rect(bounds.X + thumbAlong / 2f, y, Math.Max(0f, bounds.Width - thumbAlong), trackThickness);
            return new SliderGeometry(track, bounds.Y + band - 2f);
        }
    }

    /// <summary>
    /// Where the tick marks go along the track, as fractions 0..1: one per step, or every tenth
    /// without a step. Steps closer than 4px are thinned to every second (third, …) step.
    /// </summary>
    internal static List<float> TickFractions(float min, float max, float? step, float trackLength)
    {
        var ticks = new List<float>();
        float range = max - min;
        if (range <= 0f)
        {
            return ticks;
        }
        int intervals = step is > 0f ? (int)MathF.Round(range / step.Value) : 10;
        intervals = Math.Max(1, intervals);
        int every = 1;
        while (intervals / every > 0 && trackLength / (intervals / (float)every) < 4f)
        {
            every++;
        }
        for (int i = 0; i <= intervals; i += every)
        {
            ticks.Add(Math.Min(1f, i / (float)intervals));
        }
        if (ticks[^1] < 1f)
        {
            ticks.Add(1f);
        }
        return ticks;
    }

    /// <summary>The value one keyboard step from the current one: the step, or a hundredth of the range.</summary>
    internal float KeyboardStep => Step is > 0f ? Step.Value : (Max - Min) / 100f;

    /// <summary>Clamps to the range and snaps to the step, as a drag does.</summary>
    internal float Snap(float value)
    {
        value = Math.Clamp(value, Min, Max);
        if (Step is > 0f)
        {
            value = Math.Clamp(Min + MathF.Round((value - Min) / Step.Value) * Step.Value, Min, Max);
        }
        return value;
    }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        float currentValue = Bind.Value;
        foreach (var rule in ValidationRules)
        {
            var result = rule(currentValue);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Extension methods for <see cref="Slider"/> providing fluent modifiers.
/// </summary>
public static class SliderExtensions
{
    /// <summary>Sets a .NET format string for the value label display.</summary>
    public static Slider Format(this Slider slider, string format)
    {
        slider.FormatString = format;
        return slider;
    }

    /// <summary>Shows or hides the value label above the thumb.</summary>
    public static Slider ShowValueLabel(this Slider slider, bool show = true)
    {
        slider.ShowValueLabelValue = show;
        return slider;
    }

    /// <summary>Shows or hides tick marks along the track.</summary>
    public static Slider ShowTicks(this Slider slider, bool show = true)
    {
        slider.ShowTicksValue = show;
        return slider;
    }

    /// <summary>Sets the slider orientation.</summary>
    public static Slider Orientation(this Slider slider, Orientation orientation)
    {
        slider.OrientationValue = orientation;
        return slider;
    }

    /// <summary>Sets the slider's length in logical pixels: its width, or its height when vertical.</summary>
    public static Slider Width(this Slider slider, float width)
    {
        slider.WidthValue = width;
        return slider;
    }

    /// <summary>Adds a validation rule.</summary>
    public static Slider Validate(this Slider slider, Func<float, ValidationResult> rule)
    {
        slider.ValidationRules.Add(rule);
        return slider;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Slider ValidateOn(this Slider slider, ValidationTrigger trigger)
    {
        slider.ValidationTriggerMode = trigger;
        return slider;
    }

    /// <summary>Disables the slider.</summary>
    public static Slider Disabled(this Slider slider, bool disabled = true)
    {
        slider.IsDisabled = disabled;
        return slider;
    }

    /// <summary>Makes the slider read-only.</summary>
    public static Slider ReadOnly(this Slider slider, bool readOnly = true)
    {
        slider.IsReadOnly = readOnly;
        return slider;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Slider AccessibleLabel(this Slider slider, LocKey label)
    {
        slider.LayoutData.A11yLabel = label.Resolve();
        return slider;
    }
}

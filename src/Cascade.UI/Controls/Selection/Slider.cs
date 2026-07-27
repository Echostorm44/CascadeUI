namespace Cascade.UI;

/// <summary>
/// A single-value slider control for selecting a numeric value within a range.
/// Supports step snapping, tick marks, value labels, and custom formatting.
/// </summary>
public sealed class Slider : Node
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
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

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

    /// <summary>Sets the slider width in logical pixels.</summary>
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
        slider.AccessibleLabelValue = label;
        return slider;
    }
}

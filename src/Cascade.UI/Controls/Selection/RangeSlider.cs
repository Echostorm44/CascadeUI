namespace Cascade.UI;

/// <summary>
/// A dual-thumb slider for selecting a numeric range. Both thumbs move on the
/// same track. The track segment between the thumbs is filled. The thumbs
/// cannot cross each other.
/// </summary>
public sealed class RangeSlider : Node
{
    /// <summary>
    /// Creates a range slider with two bound values.
    /// </summary>
    /// <param name="minBind">Two-way binding to the lower bound value.</param>
    /// <param name="maxBind">Two-way binding to the upper bound value.</param>
    /// <param name="min">Minimum track value.</param>
    /// <param name="max">Maximum track value.</param>
    /// <param name="step">Step increment for snapping. Null for continuous.</param>
    /// <param name="label">Optional label displayed above the slider.</param>
    public RangeSlider(
        Bindable<float> minBind,
        Bindable<float> maxBind,
        float min = 0f,
        float max = 1f,
        float? step = null,
        LocKey label = default)
    {
        MinBind = minBind;
        MaxBind = maxBind;
        Min = min;
        Max = max;
        Step = step;
        Label = label;
    }

    /// <summary>Two-way binding to the lower bound value.</summary>
    public Bindable<float> MinBind { get; }

    /// <summary>Two-way binding to the upper bound value.</summary>
    public Bindable<float> MaxBind { get; }

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
    internal List<Func<float, float, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    /// <summary>
    /// Absolute bounds in window coordinates, set by the painter each frame.
    /// Used by InputDispatcher for accurate thumb hit detection.
    /// </summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Runs all registered validation rules against the current range values.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        float currentMin = MinBind.Value;
        float currentMax = MaxBind.Value;
        foreach (var rule in ValidationRules)
        {
            var result = rule(currentMin, currentMax);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Extension methods for <see cref="RangeSlider"/> providing fluent modifiers.
/// </summary>
public static class RangeSliderExtensions
{
    /// <summary>Sets a .NET format string for the value label display.</summary>
    public static RangeSlider Format(this RangeSlider slider, string format)
    {
        slider.FormatString = format;
        return slider;
    }

    /// <summary>Shows or hides value labels above the thumbs.</summary>
    public static RangeSlider ShowValueLabel(this RangeSlider slider, bool show = true)
    {
        slider.ShowValueLabelValue = show;
        return slider;
    }

    /// <summary>Shows or hides tick marks along the track.</summary>
    public static RangeSlider ShowTicks(this RangeSlider slider, bool show = true)
    {
        slider.ShowTicksValue = show;
        return slider;
    }

    /// <summary>Adds a validation rule.</summary>
    public static RangeSlider Validate(this RangeSlider slider, Func<float, float, ValidationResult> rule)
    {
        slider.ValidationRules.Add(rule);
        return slider;
    }

    /// <summary>Sets when validation fires.</summary>
    public static RangeSlider ValidateOn(this RangeSlider slider, ValidationTrigger trigger)
    {
        slider.ValidationTriggerMode = trigger;
        return slider;
    }

    /// <summary>Disables the slider.</summary>
    public static RangeSlider Disabled(this RangeSlider slider, bool disabled = true)
    {
        slider.IsDisabled = disabled;
        return slider;
    }

    /// <summary>Makes the slider read-only.</summary>
    public static RangeSlider ReadOnly(this RangeSlider slider, bool readOnly = true)
    {
        slider.IsReadOnly = readOnly;
        return slider;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static RangeSlider AccessibleLabel(this RangeSlider slider, LocKey label)
    {
        slider.AccessibleLabelValue = label;
        return slider;
    }
}

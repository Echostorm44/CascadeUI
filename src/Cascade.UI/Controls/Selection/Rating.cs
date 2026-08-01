namespace Cascade.UI;

/// <summary>
/// A row of icons the user clicks to set a numeric rating. Supports
/// half-stars, custom icons, custom colors, and per-value tooltip labels.
/// </summary>
public sealed class Rating : Node
{
    /// <summary>
    /// Creates a rating control bound to a float field.
    /// </summary>
    /// <param name="value">Two-way binding to the rating value.</param>
    /// <param name="max">Maximum rating value (number of icons).</param>
    /// <param name="icon">The icon used for each rating step.</param>
    /// <param name="label">Optional label displayed above or beside the rating.</param>
    public Rating(
        Bindable<float> value,
        int max = 5,
        Icon icon = default,
        LocKey label = default)
    {
        BoundValue = value;
        ReadOnlyValue = null;
        Max = max;
        DefaultIcon = icon;
        Label = label;
    }

    /// <summary>
    /// Creates a read-only rating display (e.g., in a product card).
    /// </summary>
    /// <param name="value">The fixed rating value to display.</param>
    /// <param name="max">Maximum rating value (number of icons).</param>
    /// <param name="icon">The icon used for each rating step.</param>
    /// <param name="label">Optional label displayed above or beside the rating.</param>
    public Rating(
        float value,
        int max = 5,
        Icon icon = default,
        LocKey label = default)
    {
        BoundValue = null;
        ReadOnlyValue = value;
        Max = max;
        DefaultIcon = icon;
        Label = label;
    }

    /// <summary>Two-way binding. Null when using a read-only fixed value.</summary>
    public Bindable<float>? BoundValue { get; }

    /// <summary>Fixed read-only value. Null when using a binding.</summary>
    public float? ReadOnlyValue { get; }

    /// <summary>Maximum rating value (number of icons).</summary>
    public int Max { get; }

    /// <summary>The default icon used for each rating step.</summary>
    public Icon DefaultIcon { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsReadOnly { get; set; }
    internal bool HalfStarsEnabled { get; set; }
    internal float? SizeValue { get; set; }
    internal Icon? FilledIcon { get; set; }
    internal Icon? EmptyIcon { get; set; }
    internal ColorValue? FilledColor { get; set; }
    internal ColorValue? EmptyColor { get; set; }
    internal IReadOnlyList<string>? TooltipLabels { get; set; }
    internal List<Func<float, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }

    /// <summary>
    /// Absolute bounds set by the painter during rendering.
    /// Used by InputDispatcher for click-to-star mapping.
    /// </summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        float currentValue = BoundValue?.Value ?? ReadOnlyValue ?? 0f;
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
/// Extension methods for <see cref="Rating"/> providing fluent modifiers.
/// </summary>
public static class RatingExtensions
{
    /// <summary>Makes the rating display read-only.</summary>
    public static Rating ReadOnly(this Rating rating, bool readOnly = true)
    {
        rating.IsReadOnly = readOnly;
        return rating;
    }

    /// <summary>Enables half-star rendering for fractional values.</summary>
    public static Rating HalfStars(this Rating rating, bool enabled = true)
    {
        rating.HalfStarsEnabled = enabled;
        return rating;
    }

    /// <summary>Sets the icon size in logical pixels.</summary>
    public static Rating Size(this Rating rating, float size)
    {
        rating.SizeValue = size;
        return rating;
    }

    /// <summary>Sets custom filled and empty icons.</summary>
    public static Rating Icon(this Rating rating, Icon filled, Icon empty)
    {
        rating.FilledIcon = filled;
        rating.EmptyIcon = empty;
        return rating;
    }

    /// <summary>Sets custom colors for filled and empty icons.</summary>
    public static Rating Color(this Rating rating, ColorValue filled, ColorValue empty)
    {
        rating.FilledColor = filled;
        rating.EmptyColor = empty;
        return rating;
    }

    /// <summary>Sets tooltip labels for each rating value (1-indexed).</summary>
    public static Rating Labels(this Rating rating, IReadOnlyList<string> labels)
    {
        rating.TooltipLabels = labels;
        return rating;
    }

    /// <summary>Adds a validation rule.</summary>
    public static Rating Validate(this Rating rating, Func<float, ValidationResult> rule)
    {
        rating.ValidationRules.Add(rule);
        return rating;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Rating ValidateOn(this Rating rating, ValidationTrigger trigger)
    {
        rating.ValidationTriggerMode = trigger;
        return rating;
    }

    /// <summary>Disables the rating control.</summary>
    public static Rating Disabled(this Rating rating, bool disabled = true)
    {
        rating.IsDisabled = disabled;
        return rating;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Rating AccessibleLabel(this Rating rating, LocKey label)
    {
        rating.LayoutData.A11yLabel = label.Resolve();
        return rating;
    }
}

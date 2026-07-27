namespace Cascade.UI;

/// <summary>
/// A binary switch control. Semantically equivalent to Checkbox but visually
/// communicates on/off rather than selected/deselected. Use Toggle for settings
/// and preferences; use Checkbox for form field selection and lists.
/// </summary>
public sealed class Toggle : Node
{
    /// <summary>
    /// Creates a toggle switch bound to a boolean field.
    /// </summary>
    /// <param name="value">Two-way binding to the on/off state.</param>
    /// <param name="label">Optional label describing the setting.</param>
    /// <param name="description">Optional description text below the label.</param>
    public Toggle(
        Bindable<bool> value,
        LocKey label = default,
        LocKey description = default)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    /// <summary>Two-way binding to the on/off state.</summary>
    public Bindable<bool> Value { get; }

    /// <summary>Optional label describing the setting.</summary>
    public LocKey Label { get; }

    /// <summary>Optional description text below the label.</summary>
    public LocKey Description { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal ToggleLabelPosition LabelPositionValue { get; set; } = ToggleLabelPosition.Right;
    internal List<Func<bool, ValidationResult>> ValidationRules { get; } = [];
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
        bool currentValue = Value.Value;
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
/// Extension methods for <see cref="Toggle"/> providing fluent modifiers.
/// </summary>
public static class ToggleExtensions
{
    /// <summary>Sets the label position relative to the toggle.</summary>
    public static Toggle LabelPosition(this Toggle toggle, ToggleLabelPosition position)
    {
        toggle.LabelPositionValue = position;
        return toggle;
    }

    /// <summary>Adds a validation rule.</summary>
    public static Toggle Validate(this Toggle toggle, Func<bool, ValidationResult> rule)
    {
        toggle.ValidationRules.Add(rule);
        return toggle;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Toggle ValidateOn(this Toggle toggle, ValidationTrigger trigger)
    {
        toggle.ValidationTriggerMode = trigger;
        return toggle;
    }

    /// <summary>Disables the toggle.</summary>
    public static Toggle Disabled(this Toggle toggle, bool disabled = true)
    {
        toggle.IsDisabled = disabled;
        return toggle;
    }

    /// <summary>Makes the toggle read-only.</summary>
    public static Toggle ReadOnly(this Toggle toggle, bool readOnly = true)
    {
        toggle.IsReadOnly = readOnly;
        return toggle;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Toggle AccessibleLabel(this Toggle toggle, LocKey label)
    {
        toggle.AccessibleLabelValue = label;
        return toggle;
    }
}



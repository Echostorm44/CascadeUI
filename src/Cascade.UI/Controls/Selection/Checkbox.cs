namespace Cascade.UI;

/// <summary>
/// A binary on/off selection control. Supports an indeterminate state for
/// partial selection (e.g., tree node parent with mixed children).
/// </summary>
public sealed class Checkbox : Node
{
    /// <summary>
    /// Creates a checkbox bound to a boolean field.
    /// </summary>
    /// <param name="value">Two-way binding to the checked state.</param>
    /// <param name="label">Optional label — tap anywhere on the label to toggle.</param>
    public Checkbox(
        Bindable<bool> value,
        LocKey label = default)
    {
        BoolValue = value;
        ThreeStateValue = null;
        OnChange = null;
        Label = label;
    }

    /// <summary>
    /// Creates a three-state checkbox for indeterminate scenarios.
    /// </summary>
    /// <param name="value">The current <see cref="CheckboxValue"/> state.</param>
    /// <param name="onChange">Callback invoked when the user toggles the checkbox.</param>
    /// <param name="label">Optional label.</param>
    public Checkbox(
        CheckboxValue value,
        Action<CheckboxValue> onChange,
        LocKey label = default)
    {
        BoolValue = null;
        ThreeStateValue = value;
        OnChange = onChange;
        Label = label;
    }

    /// <summary>Two-way binding for simple bool checkbox. Null when using three-state.</summary>
    public Bindable<bool>? BoolValue { get; }

    /// <summary>Three-state value. Null when using simple bool binding.</summary>
    public CheckboxValue? ThreeStateValue { get; }

    /// <summary>Callback for three-state checkbox changes.</summary>
    public Action<CheckboxValue>? OnChange { get; }

    /// <summary>Optional label text.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<bool, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    /// <summary>
    /// Runs all registered validation rules against the current boolean value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        bool currentValue = BoolValue?.Value ?? (ThreeStateValue == CheckboxValue.Checked);
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
/// Extension methods for <see cref="Checkbox"/> providing fluent modifiers.
/// </summary>
public static class CheckboxExtensions
{
    /// <summary>Adds a validation rule.</summary>
    public static Checkbox Validate(this Checkbox checkbox, Func<bool, ValidationResult> rule)
    {
        checkbox.ValidationRules.Add(rule);
        return checkbox;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Checkbox ValidateOn(this Checkbox checkbox, ValidationTrigger trigger)
    {
        checkbox.ValidationTriggerMode = trigger;
        return checkbox;
    }

    /// <summary>Disables the checkbox.</summary>
    public static Checkbox Disabled(this Checkbox checkbox, bool disabled = true)
    {
        checkbox.IsDisabled = disabled;
        return checkbox;
    }

    /// <summary>Makes the checkbox read-only.</summary>
    public static Checkbox ReadOnly(this Checkbox checkbox, bool readOnly = true)
    {
        checkbox.IsReadOnly = readOnly;
        return checkbox;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Checkbox AccessibleLabel(this Checkbox checkbox, LocKey label)
    {
        checkbox.LayoutData.A11yLabel = label.Resolve();
        return checkbox;
    }
}

/// <summary>
/// Three-state value for a <see cref="Checkbox"/> supporting indeterminate state.
/// </summary>
public enum CheckboxValue
{
    /// <summary>The checkbox is unchecked.</summary>
    Unchecked,

    /// <summary>The checkbox is checked.</summary>
    Checked,

    /// <summary>The checkbox is in an indeterminate state (partial selection).</summary>
    Indeterminate
}

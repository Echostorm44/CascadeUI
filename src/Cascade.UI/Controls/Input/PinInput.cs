namespace Cascade.UI;

/// <summary>
/// A row of individual single-character input boxes for PIN or OTP entry.
/// Each box accepts exactly one character. Focus advances automatically
/// to the next box.
/// </summary>
public sealed class PinInput : Node
{
    /// <summary>
    /// Creates a PIN/OTP input with the specified number of character boxes.
    /// </summary>
    /// <param name="value">Two-way binding to the combined PIN string.</param>
    /// <param name="length">Number of character boxes.</param>
    /// <param name="label">Optional label displayed above the input.</param>
    public PinInput(
        Bindable<string> value,
        int length,
        LocKey label = default)
    {
        Value = value;
        Length = length;
        Label = label;
    }

    /// <summary>Two-way binding to the combined PIN string.</summary>
    public Bindable<string> Value { get; }

    /// <summary>Number of character boxes.</summary>
    public int Length { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsNumericOnly { get; set; }
    internal bool IsMasked { get; set; }
    internal Action<string>? AutoSubmitHandler { get; set; }
    internal List<int> SeparatorPositions { get; } = [];
    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    /// <summary>Absolute bounds in window coordinates, set by the painter each frame.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Determines whether a character is accepted for a pin box.
    /// When numeric-only mode is on, only digits are accepted.
    /// </summary>
    internal bool AcceptsCharacter(char c)
    {
        if (IsNumericOnly)
        {
            return char.IsAsciiDigit(c);
        }

        return !char.IsControl(c);
    }

    /// <summary>
    /// Handles a paste operation by distributing characters across pin boxes.
    /// Characters beyond the length are ignored. Returns the resulting PIN string.
    /// </summary>
    internal string HandlePaste(string pastedText)
    {
        var result = new char[Length];
        int written = 0;

        foreach (char c in pastedText)
        {
            if (written >= Length)
            {
                break;
            }

            if (AcceptsCharacter(c))
            {
                result[written++] = c;
            }
        }

        return new string(result, 0, written);
    }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        string currentValue = Value.Value ?? string.Empty;
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
/// Extension methods for <see cref="PinInput"/> providing fluent modifiers.
/// </summary>
public static class PinInputExtensions
{
    /// <summary>Restricts input to digits only.</summary>
    public static PinInput Numeric(this PinInput input)
    {
        input.IsNumericOnly = true;
        return input;
    }

    /// <summary>Masks characters with a bullet symbol.</summary>
    public static PinInput Masked(this PinInput input)
    {
        input.IsMasked = true;
        return input;
    }

    /// <summary>Fires the callback when all boxes are filled.</summary>
    public static PinInput AutoSubmit(this PinInput input, Action<string> onComplete)
    {
        input.AutoSubmitHandler = onComplete;
        return input;
    }

    /// <summary>Renders a visual separator after the specified box index (1-based).</summary>
    public static PinInput Separator(this PinInput input, int after)
    {
        if (!input.SeparatorPositions.Contains(after))
        {
            input.SeparatorPositions.Add(after);
        }

        return input;
    }

    /// <summary>Adds a validation rule.</summary>
    public static PinInput Validate(this PinInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static PinInput ValidateOn(this PinInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Disables the input.</summary>
    public static PinInput Disabled(this PinInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only.</summary>
    public static PinInput ReadOnly(this PinInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static PinInput AccessibleLabel(this PinInput input, LocKey label)
    {
        input.AccessibleLabelValue = label;
        return input;
    }
}
